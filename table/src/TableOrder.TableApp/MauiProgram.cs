namespace TableOrder.TableApp;

using BunnyTail.DependencyInjection;

using CommunityToolkit.Maui;

using Fonts;

using Microsoft.Maui.LifecycleEvents;

#if false
using Plugin.Maui.DebugRainbows;
#endif

using SkiaSharp.Views.Maui.Controls.Hosting;

using Smart.Mvvm.Resolver;

using Syncfusion.Maui.Toolkit.Hosting;

using TableOrder.Client.Rest;
using TableOrder.TableApp.Components;
using TableOrder.TableApp.Modules;
using TableOrder.TableApp.Shell;
using TableOrder.Terminal.Behaviors;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Diagnostics;
using TableOrder.Terminal.Extender;
using TableOrder.Terminal.Shell;

public static partial class MauiProgram
{
    private const string ModulesNamespace = "TableOrder.TableApp.Modules";

    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseGeneratedServiceProvider()
            .ConfigureDebug()
            .ConfigureFonts(ConfigureFonts)
            .ConfigureLifecycleEvents(ConfigureLifecycleEvents)
            .ConfigureEssentials(ConfigureEssentials)
            .ConfigureLogging()
            .ConfigureGlobalSettings()
            .ConfigureSyncfusionToolkit()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit(ConfigureMauiCommunityToolkit)
            .UseMauiServices()
            .UseMauiComponents()
            .UseCommunityToolkitServices()
            .UseCustomView()
            .BuildApplication();

    // ------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------

    private static MauiAppBuilder ConfigureDebug(this MauiAppBuilder builder)
    {
#if DEBUG
#if false
        builder
            .UseDebugRainbows(new DebugRainbowsOptions
            {
                ShowRainbows = true,
                ShowGrid = true,
                HorizontalItemSize = 20,
                VerticalItemSize = 20,
                MajorGridLineInterval = 4,
                MajorGridLines = new GridLineOptions { Color = Color.FromRgb(255, 0, 0), Opacity = 0.5, Width = 3 },
                MinorGridLines = new GridLineOptions { Color = Color.FromRgb(255, 0, 0), Opacity = 0.25, Width = 1 },
                GridOrigin = DebugGridOrigin.TopLeft
            });
#endif
#endif
        return builder;
    }

    // ------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------

    private static MauiAppBuilder ConfigureLogging(this MauiAppBuilder builder)
    {
        // Debug
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Android
#if ANDROID
        builder.Logging.AddAndroidLogger(static options => options.ShortCategory = true);
#endif
        // File
        builder.Logging.AddFileLogger(static options =>
            {
#if ANDROID
                options.Directory = Path.Combine(AndroidHelper.GetExternalFilesDir(), "log");
#endif
                options.RetainDays = 7;
            })
            .AddFilter(typeof(MauiProgram).Namespace, LogLevel.Debug);

        return builder;
    }

    // ------------------------------------------------------------
    // Application
    // ------------------------------------------------------------

    // ReSharper disable UnusedParameter.Local
    private static void ConfigureLifecycleEvents(ILifecycleBuilder effects)
    {
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable UnusedParameter.Local
    private static void ConfigureEssentials(IEssentialsBuilder config)
    {
    }
    // ReSharper restore UnusedParameter.Local

    private static void ConfigureMauiCommunityToolkit(Options options)
    {
        // ポップアップは画面の中央に出す四角い面 (角丸と影を付けない)
        options.SetPopupDefaults(new DefaultPopupSettings
        {
            CanBeDismissedByTappingOutsideOfPopup = false,
            Padding = 0,
            Margin = 0
        });
        options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings
        {
            CanBeDismissedByTappingOutsideOfPopup = false,
            Shadow = null,
            Shape = null
        });
    }

    private static MauiAppBuilder ConfigureGlobalSettings(this MauiAppBuilder builder)
    {
        // TODO App center alternative

        // Crash dump
        CrashReport.Start();

        return builder;
    }

    private static MauiAppBuilder UseCustomView(this MauiAppBuilder builder)
    {
        // Behaviors
        builder.ConfigureCustomBehaviors();

#if ANDROID
        // Font icons
        builder.ConfigureImageSources(static services => services.AddService<FontImageSource>(static provider => new DirectFontImageSourceService(provider.GetRequiredService<IFontManager>())));
#endif

        return builder;
    }

    // ------------------------------------------------------------
    // Design
    // ------------------------------------------------------------

    private static void ConfigureFonts(IFontCollection fonts)
    {
        fonts.AddFont("MaterialIcons-Regular.ttf", MaterialIcons.FontFamily);
    }

    private static void ConfigureDialogDesign(DialogConfig config)
    {
        var resources = Application.Current!.Resources;
        config.IndicatorColor = resources.FindResource<Color>("PrimaryColor");
        config.LoadingMessageFontSize = 28;
        config.ProgressCircleColor1 = resources.FindResource<Color>("PrimaryColor");
        config.ProgressCircleColor2 = resources.FindResource<Color>("OutlineColor");

        // Avoiding conflicts with progress
        config.LockBackgroundColor = Colors.Transparent;
        config.LoadingBackgroundColor = Colors.Transparent;
        config.ProgressBackgroundColor = Colors.Transparent;
    }

    // ------------------------------------------------------------
    // Components
    // ------------------------------------------------------------

    private static MauiAppBuilder UseGeneratedServiceProvider(this MauiAppBuilder builder)
    {
        builder.ConfigureContainer(
            new GeneratedServiceProviderFactory(static options => options.TrackTransientDisposables = false),
            ConfigureComponents);
        return builder;
    }

    private static void ConfigureComponents(IServiceCollection services)
    {
        // View & ViewModel
        services.AddTransient<MainPage>();
        services.AddTransient<MainPageViewModel>();
        services.AddViews();
        services.AddViewModels();

        // MauiComponents
        services.AddComponentsDialog(static c =>
        {
            ConfigureDialogDesign(c);
            c.EnablePromptEnterAction = true;
            c.EnablePromptSelectAll = true;
        });
        services.AddComponentsPopup(static c =>
        {
            c.AutoRegister(DialogSource());
            c.AutoRegister(TerminalModules.DialogSource());
        });
        services.AddSingleton<IPopupPlugin, FullscreenPopupPlugin>();
        services.AddSingleton<IPopupPlugin, PopupClosePlugin>();
        services.AddComponentsScreen();
        services.AddComponentsLocation();
        services.AddComponentsSpeech();
        services.AddCommunication();

        // Messenger
        services.AddSingleton<IReactiveMessenger>(ReactiveMessenger.Default);

        // Navigator
        services.AddNavigator(static (_, config) =>
        {
            config.UseMauiNavigationProvider();
            config.AddPlugin<NavigationFeedbackPlugin>();
#if DEBUG
            config.AddPlugin<LeakDetectionPlugin>();
#endif
            config.UseIdViewMapper(static m => m.AutoRegister(ViewSource()));
        });

        // Terminal (端末の部品、端末の設定と状態、登録と状態の報告、注文サーバの登録と通知の窓口)
        services.AddTerminalComponents(new KioskOptions(typeof(AdminReceiver), typeof(MainActivity)));

        // Components
        services.AddSingleton<ImageCache>();
        services.AddSingleton<ThemeManager>();

        // Resource
        services.AddSingleton<ResourceDictionary>(static _ => Application.Current!.Resources);

        // State
        services.AddSingleton(BusyState.Default);
        services.AddSingleton<StartupState>();
        services.AddSingleton<LanguageState>();
        services.AddSingleton<MenuState>();
        services.AddSingleton<VisitState>();
        services.AddSingleton<CartState>();
        services.AddSingleton<StoreState>();

        // Service (テーブル端末の REST の窓口)
        services.AddSingleton<ITableApi, RestTableApi>();

        // Usecase
        services.AddSingleton<OrderUsecase>();

        // Shell
        services.AddSingleton<OrderEventReceiver>();
    }

    // ------------------------------------------------------------
    // Build
    // ------------------------------------------------------------

    private static MauiApp BuildApplication(this MauiAppBuilder builder)
    {
        var app = builder.Build();

        var services = app.Services;

        // Setup provider
        ResolveProvider.Default.Provider = services;

        // Start device information
        services.GetRequiredService<DeviceInformation>().Start();

        // EMM が配る設定を読み、替わったときの知らせを受け始める (設定を読む画面より先に)
        services.GetRequiredService<ManagedConfiguration>().Start();

        // 画面の言語は端末の言語の設定によらず日本語から始める
        services.GetRequiredService<LanguageState>().Reset();

        // サーバの通知を受け始める
        services.GetRequiredService<OrderEventReceiver>().Start();

        // 端末の状態の報告を始める (登録していない間は送らない)
        services.GetRequiredService<StatusReporter>().Start();

#if DEBUG
        // Diagnostics for GeneratedServiceProvider
        if (services is GeneratedServiceProvider generatedProvider)
        {
            foreach (var line in BunnyTail.DependencyInjection.Diagnostics.ServiceFactoryReportExtensions.DescribeRuntimeFallbacks(generatedProvider).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }
#endif

#if DEBUG
        // Setup navigator
        var navigator = services.GetRequiredService<INavigator>();
        navigator.Navigated += (_, args) =>
        {
            // for debug
            System.Diagnostics.Debug.WriteLine($"Navigated: [{args.Context.FromId}]->[{args.Context.ToId}] : stacked=[{navigator.StackedCount}]");
        };
#endif

        return app;
    }

    // ------------------------------------------------------------
    // View & ViewModel
    // ------------------------------------------------------------

    // ReSharper disable UnusedMethodReturnValue.Local
    [ComponentRegistration(Lifetime.Transient, "View$", Namespace = ModulesNamespace)]
    private static partial IServiceCollection AddViews(this IServiceCollection services);

    [ComponentRegistration(Lifetime.Transient, "ViewModel$", Namespace = ModulesNamespace)]
    private static partial IServiceCollection AddViewModels(this IServiceCollection services);
    // ReSharper restore UnusedMethodReturnValue.Local

    // ------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------

    [ViewSource]
    public static partial IEnumerable<KeyValuePair<ViewId, Type>> ViewSource();

    [PopupSource]
    public static partial IEnumerable<KeyValuePair<DialogId, Type>> DialogSource();
}
