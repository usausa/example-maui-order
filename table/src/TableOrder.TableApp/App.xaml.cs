namespace TableOrder.TableApp;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Terminal.Diagnostics;

#pragma warning disable CA1724
public sealed partial class App
{
    private readonly ILogger<App> log;

    private readonly IServiceProvider serviceProvider;

    public App(ILogger<App> log, IServiceProvider serviceProvider)
    {
        this.log = log;
        this.serviceProvider = serviceProvider;

        // Light theme based application
        Current!.UserAppTheme = AppTheme.Light;

        InitializeComponent();

        // Start
        CrashReport.WatchUnobserved(log);
        log.InfoApplicationStart(typeof(App).Assembly.GetName().Version, Environment.Version);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(serviceProvider.GetRequiredService<MainPage>());
    }

    // 前回の異常終了はログに残し (お客様の画面には出さない)、待たずに起動の画面に進める
    protected override void OnStart()
    {
        CrashReport.LogPrevious(log);
        serviceProvider.GetRequiredService<StartupState>().NotifyCompleted();
    }
}
#pragma warning restore CA1724
