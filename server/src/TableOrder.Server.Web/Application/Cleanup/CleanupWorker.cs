namespace TableOrder.Server.Web.Application.Cleanup;

using TableOrder.Server.Web.Application.Context;

// 古いデータを一定の間隔 (Cleanup:IntervalMinutes) で消す。残す時間を過ぎた通知と、期限を過ぎたペアリングコード・登録トークン
// 店舗のデータ (残す期間を過ぎた閉じた来店、公開し直したメニュー) は、店舗ごとに文脈を始めて消す
public sealed class CleanupWorker : BackgroundService
{
    private readonly ILogger<CleanupWorker> log;

    private readonly TimeProvider timeProvider;

    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly CleanupSetting setting;

    private readonly EventService eventService;

    private readonly DeviceEnrollmentService enrollmentService;

    private readonly CleanupService cleanupService;

    public CleanupWorker(
        ILogger<CleanupWorker> log,
        TimeProvider timeProvider,
        ApplicationServiceContextProvider contextProvider,
        CleanupSetting setting,
        EventService eventService,
        DeviceEnrollmentService enrollmentService,
        CleanupService cleanupService)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.contextProvider = contextProvider;
        this.setting = setting;
        this.eventService = eventService;
        this.enrollmentService = enrollmentService;
        this.cleanupService = cleanupService;
    }

    // 起動したときに一度消し、そのあとは間隔ごとに消す
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(setting.IntervalMinutes), timeProvider);
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

        var enrollments = await enrollmentService.DeleteExpiredAsync(now, cancellationToken);
        if (enrollments > 0)
        {
            log.InfoEnrollmentCleanup(enrollments);
        }

        var visits = 0;
        var publications = 0;
        foreach (var store in await cleanupService.GetStoreAllAsync(cancellationToken))
        {
            var (storeVisits, storePublications) = await CleanupStoreAsync(store, cancellationToken);
            visits += storeVisits;
            publications += storePublications;
        }

        if (visits > 0)
        {
            log.InfoVisitCleanup(visits);
        }

        if (publications > 0)
        {
            log.InfoMenuPublicationCleanup(publications);
        }
    }

    // 1 つの店舗で失敗しても、ほかの店舗は片付ける (片付けられなかったものは次の間隔で消す)
    private async ValueTask<(int Visits, int Publications)> CleanupStoreAsync(StoreKeyEntity store, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = contextProvider.Begin(() => new ServiceContext(timeProvider.GetUtcNow())
            {
                TenantId = store.TenantId,
                StoreId = store.StoreId
            });
            var visits = await cleanupService.DeleteClosedVisitsAsync(setting.VisitRetentionDays, setting.VisitBatchSize, cancellationToken);
            var publications = await cleanupService.DeleteMenuPublicationsAsync(setting.MenuPublicationsKept, cancellationToken);
            return (visits, publications);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            log.ErrorStoreCleanup(ex, store.TenantId, store.StoreId);
            return (0, 0);
        }
    }
}
