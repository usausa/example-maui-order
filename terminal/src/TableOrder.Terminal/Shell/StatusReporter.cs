namespace TableOrder.Terminal.Shell;

// 端末の状態 (アプリの版、電池) を 1 分ごとにサーバに報告する。登録していない間は送らない
// 送れなくても画面には出さない (つながっているかはサーバが報告の時刻で見る)
public sealed class StatusReporter : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ILogger<StatusReporter> log;

    private readonly Settings settings;

    private readonly DeviceUsecase deviceUsecase;

    private readonly CancellationTokenSource cancel = new();

    private bool started;

    public StatusReporter(
        ILogger<StatusReporter> log,
        Settings settings,
        DeviceUsecase deviceUsecase)
    {
        this.log = log;
        this.settings = settings;
        this.deviceUsecase = deviceUsecase;
    }

    public void Dispose()
    {
        cancel.Cancel();
        cancel.Dispose();
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;

        // 待たずに進めるが、止まったときはログに残す
        RunAsync(cancel.Token).ContinueWith(
            t => log.WarnStatusReportStopped(t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            if (settings.IsRegistered)
            {
                var result = await deviceUsecase.ReportStatusAsync();
                if (!result.IsSuccess)
                {
                    log.DebugStatusReportFailed(result.Status, result.ErrorCode);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(token));
    }
}
