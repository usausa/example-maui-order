namespace TableOrder.Server.Web.Application.Cleanup;

// 古いデータを一定の間隔 (Event:CleanupMinutes) で消す。残す時間を過ぎた通知と、期限を過ぎたペアリングコード
public sealed class CleanupWorker : BackgroundService
{
    private readonly ILogger<CleanupWorker> log;

    private readonly TimeProvider timeProvider;

    private readonly EventSetting setting;

    private readonly EventService eventService;

    private readonly DeviceEnrollmentService enrollmentService;

    public CleanupWorker(
        ILogger<CleanupWorker> log,
        TimeProvider timeProvider,
        EventSetting setting,
        EventService eventService,
        DeviceEnrollmentService enrollmentService)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.setting = setting;
        this.eventService = eventService;
        this.enrollmentService = enrollmentService;
    }

    // 起動したときに一度消し、そのあとは間隔ごとに消す
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(setting.CleanupMinutes), timeProvider);
        do
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // 次の間隔で消し直す
                log.ErrorCleanup(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async ValueTask CleanupAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var events = await eventService.DeleteExpiredAsync(now, cancellationToken);
        if (events > 0)
        {
            log.InfoEventCleanup(events);
        }

        var codes = await enrollmentService.DeleteExpiredAsync(now, cancellationToken);
        if (codes > 0)
        {
            log.InfoPairingCodeCleanup(codes);
        }
    }
}
