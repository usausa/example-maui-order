namespace TableOrder.Server.Web.Hubs;

using Microsoft.AspNetCore.SignalR;

using TableOrder.Contract.Events;
using TableOrder.Server.Web.Application.Context;

// コミットした通知を、店舗ごとに通し番号の順でハブのグループに送る
// 知らせのあった店舗のほか、一定の間隔ですべての店舗の通し番号を見て、ほかのサーバが書いた通知も送る (DB を共有するサーバを並べて動かすため)
public sealed class EventDispatcher : BackgroundService
{
    private readonly ILogger<EventDispatcher> log;

    private readonly TimeProvider timeProvider;

    private readonly IHubContext<StoreHub> hubContext;

    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly EventSignal signal;

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
        EventSetting setting,
        EventService eventService)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.hubContext = hubContext;
        this.contextProvider = contextProvider;
        this.signal = signal;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var signaled = await WaitSignalAsync(stoppingToken);
            try
            {
                if (signaled)
                {
                    var stores = new HashSet<(Guid TenantId, Guid StoreId)>();
                    while (signal.Reader.TryRead(out var store))
                    {
                        stores.Add(store);
                    }

                    foreach (var store in stores)
                    {
                        await DispatchAsync(store, stoppingToken);
                    }
                }
                else
                {
                    await SweepAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // 送れなかった通知は送り終えた位置を進めていないので、次の知らせか見回りで送り直す
                log.ErrorEventDispatch(ex);
            }
        }
    }

    // 知らせを待つ。間隔の間に知らせがなければ false (すべての店舗を見回る)
    private async ValueTask<bool> WaitSignalAsync(CancellationToken stoppingToken)
    {
        using var timeout = new CancellationTokenSource(sweepInterval, timeProvider);
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

    // 送り終えた位置より通し番号が進んでいる店舗 (ほかのサーバが書いた、起動のあとにできた店舗) に送る
    private async ValueTask SweepAsync(CancellationToken cancellationToken)
    {
        foreach (var sequence in await eventService.GetSequenceAllAsync(cancellationToken))
        {
            var store = (sequence.TenantId, sequence.StoreId);
            if (sequence.LastSeq > sent.GetValueOrDefault(store))
            {
                await DispatchAsync(store, cancellationToken);
            }
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

        while (true)
        {
            var deliveries = await eventService.GetPendingAsync(sent.GetValueOrDefault(store), cancellationToken);
            if (deliveries.Count == 0)
            {
                return;
            }

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
    }
}
