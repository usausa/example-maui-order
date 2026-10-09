namespace TableOrder.KitchenApp.Shell;

// 端末の状態の報告 (1 分ごと)。ブラウザは電池を読めないので、アプリの版だけを送る
public sealed class StatusReporter : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ILogger<StatusReporter> log;

    private readonly TimeProvider timeProvider;

    private readonly IDeviceApi deviceApi;

    private readonly CancellationTokenSource stopping = new();

    private bool started;

    public StatusReporter(
        ILogger<StatusReporter> log,
        TimeProvider timeProvider,
        IDeviceApi deviceApi)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.deviceApi = deviceApi;
    }

    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
    }

    // 起動を終えたら報告を始める (起動し直しても 1 つだけ動かす)
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        _ = RunAsync(stopping.Token);
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            do
            {
                var result = await deviceApi.ReportStatusAsync(new DeviceHeartbeatRequest { AppVersion = AppInfo.Version }, token);
                if (!result.IsSuccess && (result.Status != ApiStatus.Canceled))
                {
                    log.WarnApiFailed(nameof(IDeviceApi.ReportStatusAsync), result.Status, result.ErrorCode);
                }
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException)
        {
            // 止めたとき
        }
    }
}
