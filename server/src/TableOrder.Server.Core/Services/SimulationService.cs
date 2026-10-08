namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;

// 時間で進める間隔 (明細は注文かお願いからの時間、呼び出しは呼んでからと向かってからの時間、支払は始めてからの時間)
public sealed record SimulationTiming(TimeSpan Cooking, TimeSpan Ready, TimeSpan Served, TimeSpan Acknowledge, TimeSpan CallDone, TimeSpan Payment);

// 開発の環境で、スタッフ (キッチン、ホール) と決済サービスの代わりに時間で進める (端末だけで流れを確かめるため)
public sealed class SimulationService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly BackgroundAccessor backgroundAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly KitchenAccessor kitchenAccessor;

    private readonly CallAccessor callAccessor;

    private readonly PaymentAccessor paymentAccessor;

    private readonly CallService callService;

    private readonly PaymentService paymentService;

    private readonly EventService eventService;

    public SimulationService(
        ServiceContextProvider contextProvider,
        BackgroundAccessor backgroundAccessor,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        KitchenAccessor kitchenAccessor,
        CallAccessor callAccessor,
        PaymentAccessor paymentAccessor,
        CallService callService,
        PaymentService paymentService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.backgroundAccessor = backgroundAccessor;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.kitchenAccessor = kitchenAccessor;
        this.callAccessor = callAccessor;
        this.paymentAccessor = paymentAccessor;
        this.callService = callService;
        this.paymentService = paymentService;
        this.eventService = eventService;
    }

    // 進めるもののある店舗 (テナントをまたぐ)
    public ValueTask<List<StoreKeyEntity>> GetStoreAllAsync(CancellationToken cancellationToken) =>
        backgroundAccessor.QuerySimulationStoreAllAsync(cancellationToken);

    // 文脈の店舗の、時間の経った明細・呼び出し・支払を進める
    public async ValueTask AdvanceAsync(SimulationTiming timing, CancellationToken cancellationToken)
    {
        await AdvanceLinesAsync(timing, cancellationToken);
        await AdvanceCallsAsync(timing, cancellationToken);
        await AdvancePaymentsAsync(timing, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    // スタッフが運ぶ品を、作り始め・できあがり・提供と進め、明細がそろったチケットを下げる
    private async ValueTask AdvanceLinesAsync(SimulationTiming timing, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = context.Now;
        var due = (await orderAccessor.QueryProgressLineListAsync(tenantId, storeId, cancellationToken))
            .Where(x => TargetOf(x, timing, now) > x.Status)
            .ToList();
        if (due.Count == 0)
        {
            return;
        }

        await eventService.WriteAsync(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var orders = new Dictionary<Guid, HashSet<Guid>>();
            var tickets = new HashSet<Guid>();
            foreach (var line in due)
            {
                var current = await orderAccessor.QueryLineAsync(tx, tenantId, storeId, line.Id, cancellationToken);
                if ((current is null) || !await StepAsync(tx, current, TargetOf(current, timing, now), now, cancellationToken))
                {
                    continue;
                }

                if (!orders.TryGetValue(current.VisitId, out var orderIds))
                {
                    orderIds = [];
                    orders[current.VisitId] = orderIds;
                }

                orderIds.Add(current.OrderId);
                if (current.TicketId is { } ticketId)
                {
                    tickets.Add(ticketId);
                }
            }

            foreach (var (visitId, orderIds) in orders)
            {
                var visit = (await visitAccessor.QueryAsync(tx, tenantId, storeId, visitId, cancellationToken))!;
                var data = new OrderLinesUpdatedEventData
                {
                    VisitId = visitId,
                    Orders = (await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visitId, cancellationToken)).Where(x => orderIds.Contains(x.Id)).ToList()
                };
                await transaction.AppendEventAsync(EventTypes.OrderLinesUpdated, data, [visit.TableId], null, now, cancellationToken);
            }

            // 明細がそろった (できあがりか提供か取消) チケットは下げる
            foreach (var ticketId in tickets)
            {
                var lines = await kitchenAccessor.QueryTicketLineListAsync(tx, tenantId, ticketId, cancellationToken);
                if (lines.All(static x => x.Status is OrderLineStatus.Ready or OrderLineStatus.Served or OrderLineStatus.Cancelled))
                {
                    await kitchenAccessor.UpdateTicketDoneAsync(tx, tenantId, ticketId, now, cancellationToken);
                }

                var ticket = await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.TicketUpdated, ticket, null, ticket.StationId, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    // 注文かお願いからの時間で決まる状態
    private static OrderLineStatus TargetOf(OrderLineEntity line, SimulationTiming timing, DateTimeOffset now)
    {
        var elapsed = now - (line.ReleasedAt ?? now);
        if (elapsed >= timing.Served)
        {
            return OrderLineStatus.Served;
        }

        if (elapsed >= timing.Ready)
        {
            return OrderLineStatus.Ready;
        }

        return elapsed >= timing.Cooking ? OrderLineStatus.Cooking : OrderLineStatus.Ordered;
    }

    // 1 段ずつ進める (ほかの端末が先に進めた明細は、その先から進める)
    private async ValueTask<bool> StepAsync(DbTransaction tx, OrderLineEntity line, OrderLineStatus target, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var status = line.Status;
        var changed = false;
        if ((status == OrderLineStatus.Ordered) && (target >= OrderLineStatus.Cooking))
        {
            changed |= await orderAccessor.UpdateLineStartedAsync(tx, line.TenantId, line.Id, now, cancellationToken) > 0;
            status = OrderLineStatus.Cooking;
        }

        if ((status == OrderLineStatus.Cooking) && (target >= OrderLineStatus.Ready))
        {
            changed |= await orderAccessor.UpdateLineReadyAsync(tx, line.TenantId, line.Id, now, cancellationToken) > 0;
            status = OrderLineStatus.Ready;
        }

        if ((status == OrderLineStatus.Ready) && (target == OrderLineStatus.Served))
        {
            changed |= await orderAccessor.UpdateLineServedAsync(tx, line.TenantId, line.Id, null, now, cancellationToken) > 0;
        }

        return changed;
    }

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    // 呼び出しに向かい、しばらくして対応したことにする
    private async ValueTask AdvanceCallsAsync(SimulationTiming timing, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var now = context.Now;
        foreach (var call in await callAccessor.QueryOpenListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken))
        {
            if ((call.Status == CallStatus.Open) && (now - call.CreatedAt >= timing.Acknowledge))
            {
                await callService.AcknowledgeAsync(call.Id, cancellationToken);
            }
            else if ((call.Status == CallStatus.Acknowledged) && (now - (call.AcknowledgedAt ?? now) >= timing.CallDone))
            {
                await callService.DoneAsync(call.Id, cancellationToken);
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Payment
    //--------------------------------------------------------------------------------

    // 待っている支払を払い終えたことにする (QR コード決済の決済サービスと、テーブルの決済端末の代わり)
    private async ValueTask AdvancePaymentsAsync(SimulationTiming timing, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        foreach (var payment in await paymentAccessor.QueryPendingListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken))
        {
            if (context.Now - payment.CreatedAt >= timing.Payment)
            {
                await paymentService.CompleteAsync(payment.Id, cancellationToken);
            }
        }
    }
}
