namespace TableOrder.Terminal.Hall;

using BunnyTail.DependencyInjection;

using CommunityToolkit.Maui;

using Smart.Mvvm.Resolver;

using TableOrder.Terminal.Hall.Modules;
using TableOrder.Terminal.Hall.State;

public static partial class MauiProgram
{
    private const string ModulesNamespace = "TableOrder.Terminal.Hall.Modules";

    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseGeneratedServiceProvider()
            .ConfigureLogging()
            .UseMauiCommunityToolkit()
            .UseMauiServices()
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

        builder.Logging.AddFilter(typeof(MauiProgram).Namespace, LogLevel.Debug);

        return builder;
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

        // Navigator
        services.AddNavigator(static (_, config) =>
        {
            config.UseMauiNavigationProvider();
            config.UseIdViewMapper(static m => m.AutoRegister(ViewSource()));
        });

        // State
        services.AddSingleton(BusyState.Default);
        services.AddSingleton<StartupState>();
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
}
