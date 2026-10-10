namespace TableOrder.Server.Web.Application;

using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

using MudBlazor;
using MudBlazor.Services;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Images;
using TableOrder.Server.Core.Infrastructure.Json;
using TableOrder.Server.Core.Infrastructure.Payments;
using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Authentication;
using TableOrder.Server.Web.Application.Cleanup;
using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Application.ExceptionHandling;
using TableOrder.Server.Web.Application.HealthChecks;
using TableOrder.Server.Web.Application.Simulation;
using TableOrder.Server.Web.Application.Telemetry;
using TableOrder.Server.Web.Components;
using TableOrder.Server.Web.Endpoints;
using TableOrder.Server.Web.Hubs;
using TableOrder.Server.Web.Infrastructure.Logging;
using TableOrder.Server.Web.Infrastructure.Security;

public static class ApplicationExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // キッチン端末の Web アプリ (WebAssembly) を配る場所 (TableOrder.KitchenApp の StaticWebAssetBasePath と合わせる)
    private const string KitchenPath = "/kitchen";

    // 要求の経路で確かめ方 (アクセストークンか Cookie) を選ぶ既定の方式
    private const string SchemeSelector = "TableOrder";

    //--------------------------------------------------------------------------------
    // Logging
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureLogging(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        // アプリのログ。接続元と、要求のテナント・店舗・主体 (端末かクライアント) を全行に付ける (テナントごとに調べられるように)
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (provider, options) =>
            {
                // 要求の外 (裏の処理、管理画面の回線) は、始めている業務の文脈からテナントと店舗を付ける
                var accessor = provider.GetRequiredService<IHttpContextAccessor>();
                var contexts = provider.GetRequiredService<ApplicationServiceContextProvider>();
                options.ReadFrom.Configuration(builder.Configuration);
                options.Enrich.With(new CallbackEnricher("RemoteIpAddress", () => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString()));
                options.Enrich.With(new CallbackEnricher("TenantId", () => accessor.HttpContext?.User.FindFirstValue(ClaimNames.TenantId) ?? contexts.Peek()?.TenantId?.ToString()));
                options.Enrich.With(new CallbackEnricher("StoreId", () => accessor.HttpContext?.User.FindFirstValue(ClaimNames.StoreId) ?? contexts.Peek()?.StoreId?.ToString()));
                options.Enrich.With(new CallbackEnricher("Subject", () => accessor.HttpContext?.User.FindFirstValue(ClaimNames.Subject)));
            },
            writeToProviders: useOtlpExporter);

        // HTTP log。本文を出すかは登録したあとの設定 (テストのサーバの設定も効く) で決める
        builder.Services.AddHttpLogging(static _ => { });
        builder.Services.AddOptions<HttpLoggingOptions>()
            .Configure<LogSetting>(static (options, setting) =>
            {
                options.LoggingFields = HttpLoggingFields.RequestMethod |
                                        HttpLoggingFields.RequestPath |
                                        HttpLoggingFields.ResponseStatusCode |
                                        HttpLoggingFields.Duration;
                if (setting.HttpDump)
                {
                    options.LoggingFields |= HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody;
                    options.CombineLogs = true;
                    options.RequestBodyLogLimit = setting.HttpDumpLimit;
                    options.ResponseBodyLogLimit = setting.HttpDumpLimit;
                    options.MediaTypeOptions.Clear();
                    options.MediaTypeOptions.AddText("application/json");
                    options.MediaTypeOptions.AddText("application/*+json");
                }
            });

        // 秘密を運ぶ API (登録トークンとペアリングコード、署名した要求とアクセストークン、スタッフの PIN のハッシュ) の本文は出さない
        builder.Services.AddSingleton<IHttpLoggingInterceptor>(new BodyExcludingHttpLoggingInterceptor(static path =>
            path.StartsWithSegments(ApiRoutes.DevicePair, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(ApiRoutes.DeviceToken, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments(ApiRoutes.DeviceConfig, StringComparison.OrdinalIgnoreCase)));

        return builder;
    }

    public static WebApplication UseHttpLog(this WebApplication app)
    {
        var setting = app.Services.GetRequiredService<LogSetting>();
        if (setting.HttpLog)
        {
            app.UseWhen(
                static context => context.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase),
                static b => b.UseHttpLogging());
        }

        return app;
    }

    //--------------------------------------------------------------------------------
    // Http
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHttp(this IHostApplicationBuilder builder)
    {
        // Add services to the container
        builder.Services.AddHttpContextAccessor();

        // CSP nonce
        builder.Services.AddScoped<CspNonce>();

        // XForward
        builder.Services.Configure<ForwardedHeadersOptions>(static options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Do not restrict to local network/proxy
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        // HSTS
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // Headers. The nonce admits the import map that Blazor renders inline, MudBlazor needs inline styles,
        // the kitchen app (WebAssembly) needs 'wasm-unsafe-eval' to compile the runtime,
        // dotnet watch / Browser Link load their script from another localhost port and connect back to it
        var development = app.Environment.IsDevelopment();
        var scriptSources = development ? "'self' 'wasm-unsafe-eval' http://localhost:*" : "'self' 'wasm-unsafe-eval'";
        var connectSources = development ? "'self' http://localhost:* ws://localhost:* wss://localhost:*" : "'self'";
        app.UseMiddleware<SecurityHeadersMiddleware>(new SecurityHeadersOption
        {
            ReportOnly = app.Services.GetRequiredService<CspSetting>().ReportOnly,
            ContentSecurityPolicy = $"default-src 'self'; base-uri 'self'; object-src 'none'; form-action 'self'; frame-ancestors 'none'; img-src 'self' data:; font-src 'self'; style-src 'self' 'unsafe-inline'; script-src {scriptSources} 'nonce-{{nonce}}'; connect-src {connectSources}"
        });

        return app;
    }

    //--------------------------------------------------------------------------------
    // API
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureApi(this IHostApplicationBuilder builder)
    {
        // JSON は DB に持つ JSON (公開されたメニュー) と同じ形にし、知らない項目や重なった項目は受けない
        builder.Services.ConfigureHttpJsonOptions(static options =>
        {
            JsonDefaults.Apply(options.SerializerOptions);
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.AllowDuplicateProperties = false;
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });

        // Error handler
        builder.Services.AddProblemDetails(static options =>
        {
            options.CustomizeProblemDetails = static context =>
            {
                context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

                // 引数の結び付けと JSON の読み込みの失敗 (形の誤り、知らない項目) も入力の誤りとして errorCode を付ける (端末が扱いを決められるように)
                if ((context.ProblemDetails.Status == StatusCodes.Status400BadRequest) &&
                    context.HttpContext.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase))
                {
                    context.ProblemDetails.Extensions.TryAdd("errorCode", ErrorCodes.ValidationError);
                }
            };
        });
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        // 通知のハブ。通知の中身は API の応答と同じ形にする
        builder.Services.AddSignalR()
            .AddJsonProtocol(static options => JsonDefaults.Apply(options.PayloadSerializerOptions));

        return builder;
    }

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        // API: ProblemDetails
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler();
                b.UseStatusCodePages();
            });

        // Page: error page
        app.UseWhen(
            static context => !context.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler("/error", createScopeForErrors: true);
            });

        return app;
    }

    //--------------------------------------------------------------------------------
    // Authentication
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureAuthentication(this IHostApplicationBuilder builder)
    {
        // 確かめ方は経路で分ける (端末の API と通知のハブはアクセストークン、管理画面は Cookie。端末の API に Cookie を通さない)
        builder.Services
            .AddAuthentication(static options =>
            {
                options.DefaultScheme = SchemeSelector;
                options.DefaultChallengeScheme = SchemeSelector;
            })
            .AddPolicyScheme(SchemeSelector, null, static options =>
            {
                options.ForwardDefaultSelector = static context =>
                    IsDeviceRequest(context.Request.Path) ? JwtBearerDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme;
            })
            .AddJwtBearer()
            .AddIdentityCookies();

        // 署名の鍵は SigningKeyProvider が持つので、検証の設定は登録した部品から組み立てる
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TokenSetting, SigningKeyProvider, RevocationList>(static (options, setting, keys, revocations) =>
            {
                // クレームの名前を変えない (tenant_id などをトークンの名前のまま読む)
                options.MapInboundClaims = false;

                // 401 の WWW-Authenticate に失敗の理由 (期限切れ、署名の誤り) を付けない (理由で見分けさせない)
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = setting.Issuer,
                    ValidAudience = setting.Audience,
                    IssuerSigningKey = keys.SecurityKey,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Subject
                };

                // WebSocket はヘッダを付けられない (ブラウザのキッチン端末) ので、ハブだけクエリのトークンも受ける
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = static context =>
                    {
                        if (context.Request.Path.StartsWithSegments(ApiRoutes.StoreHub, StringComparison.OrdinalIgnoreCase) &&
                            (context.Request.Query["access_token"].ToString() is { Length: > 0 } token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },

                    // 無効にした端末と止めたテナントのトークンは、期限の前でもすぐに断る (401。端末はトークンを取り直して、断られた理由を知る)
                    OnTokenValidated = context =>
                    {
                        if (Guid.TryParse(context.Principal?.FindFirstValue(ClaimNames.TenantId), out var tenantId) &&
                            Guid.TryParse(context.Principal?.FindFirstValue(ClaimNames.Subject), out var deviceId) &&
                            revocations.IsRevoked(tenantId, deviceId))
                        {
                            context.Fail("The access token is revoked.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        // 管理画面の利用者 (ASP.NET Core Identity の部品を、自前の表と Service で使う)
        builder.Services
            .AddIdentityCore<AdminUserEntity>(static options =>
            {
                // パスワードは長さで強さを決め、文字の種類は問わない
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // 5 回続けて間違えたら 15 分止める
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                // 利用者名はメールアドレスで、形は足すときに確かめる。Identity の文字の制限は外す
                // (制限に合わないアドレスの利用者が、パスワードを替えられず、間違えた回数も書けなくならないように)
                options.User.AllowedUserNameCharacters = string.Empty;

                // 利用者の id は端末と同じクレームの名前にする (ログに主体として出す)
                options.ClaimsIdentity.UserIdClaimType = ClaimNames.Subject;
            })
            .AddUserStore<AdminUserStore>()
            .AddSignInManager<AdminSignInManager>()
            .AddClaimsPrincipalFactory<AdminClaimsPrincipalFactory>()
            .AddTokenProvider<AuthenticatorTokenProvider<AdminUserEntity>>(TokenOptions.DefaultAuthenticatorProvider);

        // サインインは 8 時間保ち、使っていれば延ばす。ほかのサイトからの遷移では Cookie を送らない
        builder.Services.ConfigureApplicationCookie(static options =>
        {
            options.Cookie.Name = "TableOrder.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = AccountPaths.SignIn;
            options.LogoutPath = AccountPaths.SignOut;
            options.AccessDeniedPath = AccountPaths.AccessDenied;
        });

        // 資格の印を 1 分ごとに確かめる (止めた利用者や役割を替えた利用者のサインインを長く残さない)
        builder.Services.Configure<SecurityStampValidatorOptions>(static options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        // 端末の種類で使える API を絞り (範囲の外は 403 DEVICE_SCOPE)、管理画面は役割で開ける画面を絞る
        builder.Services.AddAuthorization(static options =>
        {
            options.AddPolicy(Policies.AnyDevice, DevicePolicy(DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen, DeviceKind.Reception));
            options.AddPolicy(Policies.MenuReader, DevicePolicy(DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen));
            options.AddPolicy(Policies.StockWriter, DevicePolicy(DeviceKind.Hall, DeviceKind.Kitchen));
            options.AddPolicy(Policies.TableReader, DevicePolicy(DeviceKind.Hall, DeviceKind.Reception));
            options.AddPolicy(Policies.VisitOpener, DevicePolicy(DeviceKind.Hall, DeviceKind.Reception, DeviceKind.Table));
            options.AddPolicy(Policies.VisitReader, DevicePolicy(DeviceKind.Table, DeviceKind.Hall));
            options.AddPolicy(Policies.TableDevice, DevicePolicy(DeviceKind.Table));
            options.AddPolicy(Policies.HallDevice, DevicePolicy(DeviceKind.Hall));
            options.AddPolicy(Policies.KitchenDevice, DevicePolicy(DeviceKind.Kitchen));
            options.AddPolicy(AdminPolicies.SignedIn, AdminPolicy(AdminRole.Operator, AdminRole.TenantAdmin, AdminRole.StoreStaff));
            options.AddPolicy(AdminPolicies.TenantAdmin, AdminPolicy(AdminRole.Operator, AdminRole.TenantAdmin));
            options.AddPolicy(AdminPolicies.Operator, AdminPolicy(AdminRole.Operator));
        });
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

        return builder;
    }

    private static bool IsDeviceRequest(PathString path) =>
        path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(ApiRoutes.StoreHub, StringComparison.OrdinalIgnoreCase);

    private static AuthorizationPolicy DevicePolicy(params DeviceKind[] kinds) =>
        new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimNames.DeviceKind, kinds.Select(static x => x.ToString()))
            .Build();

    private static AuthorizationPolicy AdminPolicy(params AdminRole[] roles) =>
        new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(AdminClaimNames.Role, roles.Select(static x => x.ToString()))
            .Build();

    //--------------------------------------------------------------------------------
    // Rate limit
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureRateLimiter(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(static options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // いつ送り直せばよいかを Retry-After (秒) で知らせる
            options.OnRejected = static (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
        });

        // 匿名で受ける端末の登録は、接続元ごとに 1 分の回数を限る (推測できるコードを試させない)
        builder.Services.AddOptions<RateLimiterOptions>()
            .Configure<RateLimitSetting>(static (options, setting) =>
            {
                options.AddPolicy(RateLimits.Pairing, context => RateLimitPartition.GetFixedWindowLimiter(
                    RateLimits.PartitionOf(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = setting.PairingPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                // 管理画面のサインインは、送信 (POST) だけを接続元ごとに 1 分の回数で限る (画面を開くのは限らない)
                // 失敗を数えて止めるのは利用者ごとなので、まとめて送って試す数をここで抑える
                // 区分の名前は送信と画面で分ける (同じ名前だと、先に作った区分の限り方を使い回す)
                options.AddPolicy(RateLimits.SignIn, context => HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"post:{RateLimits.PartitionOf(context.Connection.RemoteIpAddress)}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = setting.SignInPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetNoLimiter("page"));
            });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Compress
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureCompression(this IHostApplicationBuilder builder)
    {
        builder.Services.AddResponseCompression(static options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.AddRequestDecompression();

        return builder;
    }

    public static WebApplication UseCompression(this WebApplication app)
    {
        var setting = app.Services.GetRequiredService<CompressionSetting>();
        if (setting.Response || setting.Request)
        {
            app.UseWhen(
                static context => context.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase),
                b =>
                {
                    if (setting.Response)
                    {
                        b.UseResponseCompression();
                    }

                    if (setting.Request)
                    {
                        b.UseRequestDecompression();
                    }
                });
        }

        return app;
    }

    //--------------------------------------------------------------------------------
    // OpenApi
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureOpenApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(static options =>
        {
            options.AddDocumentTransformer(static (document, _, _) =>
            {
                document.Info.Title = "TableOrder API";
                document.Info.Version = "v1";
                document.Info.Description = "Order server API for table, hall, kitchen and reception terminals.";
                return Task.CompletedTask;
            });
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Blazor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureBlazor(this IHostApplicationBuilder builder)
    {
        // Razor components
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = builder.Environment.IsDevelopment();
            });

        // Error boundary logging
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Web.IErrorBoundaryLogger, ErrorBoundaryLogger>();

        // サインインの状態を画面に渡し、開いている回線のサインインを確かめ直す
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthenticationStateProvider, AdminAuthenticationStateProvider>();

        // MudBlazor
        builder.Services.AddMudServices(static options =>
        {
            options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            options.SnackbarConfiguration.PreventDuplicates = true;
            options.SnackbarConfiguration.NewestOnTop = false;
            options.SnackbarConfiguration.ShowCloseIcon = true;
            options.SnackbarConfiguration.VisibleStateDuration = 5000;
            options.SnackbarConfiguration.SnackbarVariant = MudBlazor.Variant.Filled;
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Health
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHealth(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck<DatabaseHealthCheck>("database");

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Telemetry
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureTelemetry(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(config =>
            {
                config.AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName);
            });

        if (useOtlpExporter)
        {
            // Log
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });
            builder.Services.Configure<OpenTelemetryLoggerOptions>(static logging =>
            {
                logging.AddOtlpExporter();
            });

            // Metrics
            telemetry
                .WithMetrics(static metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddApplicationInstrumentation()
                        .AddOtlpExporter();
                });

            // Trace
            telemetry
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation(static options =>
                        {
                            options.Filter = static context =>
                            {
                                var path = context.Request.Path;
                                return !path.StartsWithSegments(AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments(HealthEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments(KitchenPath, StringComparison.OrdinalIgnoreCase);
                            };
                        })
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter();
                });
        }

        // Custom instrument
        builder.Services.AddApplicationInstrument();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Components
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureComponents(this IHostApplicationBuilder builder)
    {
        // System
        builder.Services.AddSingleton(TimeProvider.System);

        // Data
        builder.Services.AddSingleton<IDbProvider>(static p =>
        {
            var connectionString = p.GetRequiredService<IConfiguration>().GetConnectionString("Default");
            return new DelegateDbProvider(() => new SqliteConnection(connectionString));
        });
        // 重なりは主キー (1555) と一意 (2067) の違反だけにする (外部キーや NOT NULL の違反は作りの誤りとして例外のままにする)
        builder.Services.AddSingleton<IDialect>(new DelegateDialect(
            static ex => ex is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 },
            static x => Regex.Replace(x, @"[%_\\]", @"\$0")));
        builder.Services.AddDataAccessors(typeof(DataProfile).Assembly);

        // Cache (トークンの要求の使い捨ての確かめ)
        builder.Services.AddMemoryCache();

        // Security
        builder.Services.AddSingleton<SigningKeyProvider>();
        builder.Services.AddSingleton<AccessTokenService>();
        builder.Services.AddSingleton<DeviceAssertionValidator>();

        // Revocation (無効にした端末と止めたテナントのトークンをすぐに拒む一覧。数秒ごとに読み直し、通知の接続も切る)
        builder.Services.AddSingleton<StoreHubConnections>();
        builder.Services.AddSingleton<RevocationList>();
        builder.Services.AddSingleton<IRevocationList>(static p => p.GetRequiredService<RevocationList>());
        builder.Services.AddHostedService<RevocationWorker>();

        // Service
        builder.Services.AddSingleton<ApplicationServiceContextProvider>();
        builder.Services.AddSingleton<ServiceContextProvider>(static p => p.GetRequiredService<ApplicationServiceContextProvider>());
        builder.Services.AddScoped<AdminScope>();
        builder.Services.AddScoped<StoreSelection>();
        builder.Services.AddScoped<BlazorServiceScope>();

        builder.Services.AddCoreServices();

        // Event (業務の処理が知らせた店舗の通知を、ハブで送る。送ったことは店舗を見ている管理画面にも知らせる)
        builder.Services.AddSingleton<EventSignal>();
        builder.Services.AddSingleton<IEventPublisher>(static p => p.GetRequiredService<EventSignal>());
        builder.Services.AddSingleton<StoreActivity>();
        builder.Services.AddHostedService<EventDispatcher>();

        // Cleanup (古いデータを一定の間隔で消す)
        builder.Services.AddHostedService<CleanupWorker>();

        // Payment (本物の決済サービスにつなぐまでは仮の決済サービス)
        builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

        // Image (料理の写真とチェーンのロゴの置き場。本番の置き場を作るまではファイル)
        builder.Services.AddSingleton<IImageStore>(static p => new FileImageStore(Path.Combine(AppContext.BaseDirectory, p.GetRequiredService<ImageSetting>().Directory)));

        // Simulation (開発の環境で、スタッフと決済サービスの代わりに時間で進める。動かすかは設定で決める)
        builder.Services.AddHostedService<SimulationWorker>();

        // Setting
        builder.Services.AddSetting<AdminSetting>("Admin");
        builder.Services.AddSetting<TokenSetting>("Token");
        builder.Services.AddSetting<DatabaseSetting>("Database");
        builder.Services.AddSetting<ImageSetting>("Image");
        builder.Services.AddSetting<RateLimitSetting>("RateLimit");
        builder.Services.AddSetting<EventSetting>("Event");
        builder.Services.AddSetting<SimulationSetting>("Simulation");
        builder.Services.AddSetting<CompressionSetting>("Compression");
        builder.Services.AddSetting<CspSetting>("Csp");
        builder.Services.AddSetting<LogSetting>("Log");
        builder.Services.AddSetting<TelemetrySetting>("Telemetry");

        return builder;
    }

    // 設定は検証してから値を Singleton で登録する (業務の部品に IOptions を渡さない)
    private static void AddSetting<T>(this IServiceCollection services, string section)
        where T : class
    {
        services.AddOptions<T>().BindConfiguration(section).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(static p => p.GetRequiredService<IOptions<T>>().Value);
    }

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    public static void LogStartupInformation(this WebApplication app)
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);

        var version = typeof(Program).Assembly.GetName().Version;
        var otelEndpoint = app.Configuration.GetOtelExporterEndpoint();

        app.Logger.InfoServiceStart();
        app.Logger.InfoServiceSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        app.Logger.InfoServiceSettingsEnvironment(version, Environment.CurrentDirectory);
        app.Logger.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
        app.Logger.InfoServiceSettingsThreadPool(workerThreads, completionPortThreads);
        app.Logger.InfoServiceSettingsTelemetry(otelEndpoint);
    }

    //--------------------------------------------------------------------------------
    // Kitchen
    //--------------------------------------------------------------------------------

    public static WebApplication UseKitchenFiles(this WebApplication app)
    {
        // キッチン端末の Web アプリ (WebAssembly) の _framework を、圧縮したファイルと形式を合わせて返す
        app.UseBlazorFrameworkFiles(KitchenPath);
        app.UseStaticFiles(KitchenFileOptions());

        return app;
    }

    // キッチン端末の Web アプリの名前の替わらないファイル (index.html、JavaScript の部品、スタイル) は、ブラウザに毎回確かめさせる
    // (Cache-Control がないとブラウザが推して残し、更新したあとも古い JavaScript の部品を使う。_framework は内容で名前が替わる)
    private static StaticFileOptions KitchenFileOptions() =>
        new()
        {
            OnPrepareResponse = static context =>
            {
                var path = context.Context.Request.Path;
                if (path.StartsWithSegments(KitchenPath, StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWithSegments(KitchenPath + "/_framework", StringComparison.OrdinalIgnoreCase))
                {
                    context.Context.Response.Headers.CacheControl = "no-cache";
                }
            }
        };

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Develop
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            // NSwag UI using MapOpenApi generated specification
            app.UseSwaggerUi(static options =>
            {
                options.DocumentPath = "/openapi/v1.json";
            });
        }

        // Static assets
        app.MapStaticAssets();

        // 管理画面 (サインインの画面のほかはサインインを求める。役割で絞る画面は画面に付けたポリシーで確かめる)
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode(static options =>
            {
                options.ContentSecurityFrameAncestorsPolicy = "'none'";
            })
            .RequireAuthorization(AdminPolicies.SignedIn);
        app.MapAccountEndpoints();

        // キッチン端末 (WebAssembly)。画面の経路は index.html に戻す
        app.MapFallbackToFile(KitchenPath + "/{*path:nonfile}", "kitchen/index.html", KitchenFileOptions());

        // API
        app.MapDeviceEndpoints();
        app.MapStoreEndpoints();
        app.MapTableEndpoints();
        app.MapMenuEndpoints();
        app.MapImageEndpoints();
        app.MapStockEndpoints();
        app.MapVisitEndpoints();
        app.MapOrderEndpoints();
        app.MapKitchenEndpoints();
        app.MapServingEndpoints();
        app.MapCallEndpoints();
        app.MapBillEndpoints();
        app.MapPaymentEndpoints();
        app.MapEventEndpoints();

        // 通知のハブ (アクセストークンの期限が来た接続は切る。端末は新しいトークンでつなぎ直す)
        app.MapHub<StoreHub>(ApiRoutes.StoreHub, static options => options.CloseOnAuthenticationExpiration = true);

        // API の知らない経路は 404 の Problem Details にする
        app.MapFallback(ApiRoutes.Root + "/{**path}", static () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));

        // Health
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = static r => r.Tags.Contains("live")
        });

        return app;
    }

    //--------------------------------------------------------------------------------
    // Startup
    //--------------------------------------------------------------------------------

    public static async ValueTask InitializeApplicationAsync(this WebApplication app)
    {
        // Prepare instrument
        app.Services.GetRequiredService<ApplicationInstrument>();

        // 署名の鍵を先に用意する (本番の環境で鍵がなければ起動を止める)
        app.Services.GetRequiredService<SigningKeyProvider>();

        // Prepare database (schema from the SQL file)
        var database = app.Services.GetRequiredService<DatabaseService>();
        await database.InitializeAsync(AssetPaths.Schema, CancellationToken.None);

        // サンプルのデータ (開発の環境とテスト)。料理の写真は、まだ置いていないテナントの置き場に起動のたびに写す
        if (app.Services.GetRequiredService<DatabaseSetting>().SampleData)
        {
            if (await database.LoadSampleDataAsync(AssetPaths.SampleData, AssetPaths.SampleMenu, AssetPaths.SampleWashokuMenu, CancellationToken.None))
            {
                app.Logger.InfoSampleDataLoaded();
            }

            var images = app.Services.GetRequiredService<ImageService>();
            foreach (var tenant in await app.Services.GetRequiredService<TenantService>().GetAllAsync(CancellationToken.None))
            {
                var copied = await images.CopySampleAsync(tenant.Id, AssetPaths.SampleImages, CancellationToken.None);
                if (copied > 0)
                {
                    app.Logger.InfoSampleImagesCopied(tenant.Code, copied);
                }
            }
        }

        // 運営者がひとりもいなければ、設定のメールアドレスと仮のパスワードで初めの運営者を作る
        await app.CreateInitialOperatorAsync();
    }

    private static async ValueTask CreateInitialOperatorAsync(this WebApplication app)
    {
        var account = app.Services.GetRequiredService<AccountService>();
        if (await account.HasOperatorAsync(CancellationToken.None))
        {
            return;
        }

        var setting = app.Services.GetRequiredService<AdminSetting>();
        if (String.IsNullOrEmpty(setting.InitialOperatorEmail) || String.IsNullOrEmpty(setting.InitialOperatorPassword))
        {
            app.Logger.WarnNoOperator();
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUserEntity>>();
        var operatorUser = new AdminUserEntity { Email = setting.InitialOperatorEmail };
        foreach (var validator in userManager.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(userManager, operatorUser, setting.InitialOperatorPassword)).Succeeded)
            {
                app.Logger.WarnInitialOperatorPasswordInvalid();
                return;
            }
        }

        var hash = userManager.PasswordHasher.HashPassword(operatorUser, setting.InitialOperatorPassword);
        if (await account.CreateInitialOperatorAsync(setting.InitialOperatorEmail, userManager.NormalizeName(setting.InitialOperatorEmail), "運営者", hash, CancellationToken.None) is { } userId)
        {
            app.Logger.InfoInitialOperatorCreated(userId);
        }
    }

    //--------------------------------------------------------------------------------
    // Configuration
    //--------------------------------------------------------------------------------

    private static bool IsOtelExporterEnabled(this IConfiguration configuration) =>
        !String.IsNullOrWhiteSpace(configuration.GetOtelExporterEndpoint());

    private static string GetOtelExporterEndpoint(this IConfiguration configuration) =>
        configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? string.Empty;
}
