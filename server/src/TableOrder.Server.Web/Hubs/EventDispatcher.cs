namespace TableOrder.Server.Web.Hubs;

using Microsoft.AspNetCore.SignalR;

using TableOrder.Contract.Events;
using TableOrder.Server.Web.Application.Context;

// コミットした通知を、店舗ごとに通し番号の順でハブのグループに送り、店舗を見ている管理画面にも送ったことを知らせる
// 知らせのあった店舗のほか、一定の間隔ですべての店舗の通し番号を見て、ほかのサーバが書いた通知も送る (DB を共有するサーバを並べて動かすため)
public sealed class EventDispatcher : BackgroundService
{
    private readonly ILogger<EventDispatcher> log;

    private readonly TimeProvider timeProvider;

    private readonly IHubContext<StoreHub> hubContext;

    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly EventSignal signal;

    private readonly StoreActivity activity;

    private readonly EventService eventService;

    private readonly TimeSpan sweepInterval;

    // 店舗ごとの送り終えた通し番号
    private readonly Dictionary<(Guid TenantId, Guid StoreId), long> sent = [];

    public EventDispatcher(
        ILogger<EventDispatcher> log,
        TimeProvider timeProvider,
        IHubContext<StoreHub> hubContext,
        ApplicationServiceContextProvider contextProvider,
        EventSignal signal,
        StoreActivity activity,
        EventSetting setting,
        EventService eventService)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.hubContext = hubContext;
        this.contextProvider = contextProvider;
        this.signal = signal;
        this.activity = activity;
        this.eventService = eventService;
        sweepInterval = TimeSpan.FromSeconds(setting.SweepSeconds);
    }

    // 起動より前の通知は送らない (つなぎ直した端末が GET /events で受け取る)。起動を終える前に送り始める位置を決める
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var sequence in await eventService.GetSequenceAllAsync(cancellationToken))
        {
            sent[(sequence.TenantId, sequence.StoreId)] = sequence.LastSeq;
        }

        await base.StartAsync(cancellationToken);
    }

    // 見回りは知らせの合間ではなく、前の見回りから間隔ごとに行う (知らせが続くサーバでも、ほかのサーバが書いた通知を送る)
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweepAt = timeProvider.GetUtcNow() + sweepInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await WaitSignalAsync(sweepAt - timeProvider.GetUtcNow(), stoppingToken))
            {
                var stores = new HashSet<(Guid TenantId, Guid StoreId)>();
                while (signal.Reader.TryRead(out var store))
                {
                    stores.Add(store);
                }

                foreach (var store in stores)
                {
                    await TryDispatchAsync(store, stoppingToken);
                }
            }

            if (timeProvider.GetUtcNow() >= sweepAt)
            {
                await SweepAsync(stoppingToken);
                sweepAt = timeProvider.GetUtcNow() + sweepInterval;
            }
        }
    }

    // 次の見回りまで知らせを待つ。知らせがないまま見回りの時刻になれば false
    private async ValueTask<bool> WaitSignalAsync(TimeSpan wait, CancellationToken stoppingToken)
    {
        if (wait <= TimeSpan.Zero)
        {
            return false;
        }

        using var timeout = new CancellationTokenSource(wait, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
        try
        {
            return await signal.Reader.WaitToReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    // 送り終えた位置より通し番号が進んでいる店舗 (ほかのサーバが書いた、起動のあとにできた、送れなかった店舗) に送る
    private async ValueTask SweepAsync(CancellationToken cancellationToken)
    {
        List<EventSequenceEntity> sequences;
        try
        {
            sequences = await eventService.GetSequenceAllAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            log.ErrorEventDispatch(ex);
            return;
        }

        foreach (var sequence in sequences)
        {
            var store = (sequence.TenantId, sequence.StoreId);
            if (sequence.LastSeq > sent.GetValueOrDefault(store))
            {
                await TryDispatchAsync(store, cancellationToken);
            }
        }
    }

    // 1 つの店舗で失敗しても、ほかの店舗には送る (送れなかった通知は送り終えた位置を進めていないので、次の知らせか見回りで送り直す)
    private async ValueTask TryDispatchAsync((Guid TenantId, Guid StoreId) store, CancellationToken cancellationToken)
    {
        try
        {
            await DispatchAsync(store, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            log.ErrorStoreEventDispatch(ex, store.TenantId, store.StoreId);
        }
    }

    // 店舗の送っていない通知を、通し番号の順に送る
    private async ValueTask DispatchAsync((Guid TenantId, Guid StoreId) store, CancellationToken cancellationToken)
    {
        using var scope = contextProvider.Begin(() => new ServiceContext(timeProvider.GetUtcNow())
        {
            TenantId = store.TenantId,
            StoreId = store.StoreId
        });

        var dispatched = false;
        while (true)
        {
            var deliveries = await eventService.GetPendingAsync(sent.GetValueOrDefault(store), cancellationToken);
            if (deliveries.Count == 0)
            {
                break;
            }

            dispatched = true;

            foreach (var delivery in deliveries)
            {
                var groups = StoreHubGroups.For(store.TenantId, store.StoreId, delivery);
                if (groups.Count > 0)
                {
                    await hubContext.Clients.Groups(groups).SendAsync(HubMethods.Event, delivery.Item, cancellationToken);
                }

                sent[store] = delivery.Item.Seq;
            }
        }

        if (dispatched)
        {
            activity.Notify(store.TenantId, store.StoreId);
        }
    }
}
