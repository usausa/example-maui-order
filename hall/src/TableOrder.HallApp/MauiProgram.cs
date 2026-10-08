namespace TableOrder.HallApp;

using BunnyTail.DependencyInjection;

using CommunityToolkit.Maui;

using Fonts;

using Smart.Mvvm.Resolver;

using TableOrder.Client.Rest;
using TableOrder.HallApp.Modules;
using TableOrder.HallApp.Shell;
using TableOrder.Terminal.Behaviors;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Diagnostics;
using TableOrder.Terminal.Extender;
using TableOrder.Terminal.Shell;

public static partial class MauiProgram
{
    private const string ModulesNamespace = "TableOrder.HallApp.Modules";

    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseGeneratedServiceProvider()
            .ConfigureFonts(ConfigureFonts)
            .ConfigureLogging()
            .ConfigureGlobalSettings()
            .UseMauiCommunityToolkit(ConfigureMauiCommunityToolkit)
            .UseMauiServices()
            .UseMauiComponents()
            .UseCommunityToolkitServices()
            .UseCustomView()
            .BuildApplication();

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
        // Crash dump
        CrashReport.Start();

        return builder;
    }

    private static MauiAppBuilder UseCustomView(this MauiAppBuilder builder)
    {
        // Behaviors
        builder.ConfigureCustomBehaviors();

        return builder;
    }

    // ------------------------------------------------------------
    // Design
    // ------------------------------------------------------------

    private static void ConfigureFonts(IFontCollection fonts)
    {
        fonts.AddFont("MaterialIcons-Regular.ttf", MaterialIcons.FontFamily);
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
        services.AddComponentsPopup(static c =>
        {
            c.AutoRegister(DialogSource());
            c.AutoRegister(TerminalModules.DialogSource());
        });
        services.AddSingleton<IPopupPlugin, FullscreenPopupPlugin>();
        services.AddSingleton<IPopupPlugin, PopupClosePlugin>();
        services.AddComponentsScreen();

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
        services.AddTerminalComponents(new TerminalOptions(DeviceKind.Hall), new KioskOptions(typeof(AdminReceiver), typeof(MainActivity)));

        // State
        services.AddSingleton(BusyState.Default);
        services.AddSingleton<StartupState>();
        services.AddSingleton<StoreState>();
        services.AddSingleton<MenuState>();
        services.AddSingleton<TableState>();
        services.AddSingleton<CallState>();
        services.AddSingleton<ServingState>();

        // Service (ホール端末の REST の窓口)
        services.AddSingleton<IHallApi, RestHallApi>();

        // Usecase
        services.AddSingleton<HallUsecase>();

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
