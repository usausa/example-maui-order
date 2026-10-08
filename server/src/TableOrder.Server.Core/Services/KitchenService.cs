namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Contract.Kitchen;
using TableOrder.Server.Core.Accessors;

public sealed class KitchenService
{
    // 下げたチケットを返す数 (押し間違いを戻すための直近のもの)
    private const int DoneLimit = 20;

    private readonly ServiceContextProvider contextProvider;

    private readonly OrderAccessor orderAccessor;

    private readonly KitchenAccessor kitchenAccessor;

    private readonly EventService eventService;

    public KitchenService(
        ServiceContextProvider contextProvider,
        OrderAccessor orderAccessor,
        KitchenAccessor kitchenAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.orderAccessor = orderAccessor;
        this.kitchenAccessor = kitchenAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // 受け持つ持ち場のチケット。Open は古い順、Done は下げた新しい順に直近のもの。持ち場を送らなければ受け持つすべて
    public async ValueTask<ServiceResult<KitchenTicketListResponse>> GetTicketsAsync(Guid? stationId, string? status, CancellationToken cancellationToken)
    {
        var filter = KitchenTicketStatus.Open;
        if (!String.IsNullOrEmpty(status) && (!Enum.TryParse(status, true, out filter) || !Enum.IsDefined(filter)))
        {
            return new(ServiceError.Validation("status", "Open か Done を送ってください"));
        }

        var context = contextProvider.Current;
        if ((stationId is { } requested) && !context.StationIds.Contains(requested))
        {
            return new(ServiceError.DeviceScope);
        }

        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var stations = stationId is { } id ? [id] : context.StationIds;
        if (filter == KitchenTicketStatus.Open)
        {
            var tickets = (await kitchenAccessor.QueryOpenTicketListAsync(tenantId, storeId, cancellationToken)).Where(x => stations.Contains(x.StationId)).ToList();
            var lines = (await kitchenAccessor.QueryOpenTicketLineListAsync(tenantId, storeId, cancellationToken)).ToLookup(static x => x.TicketId);
            var options = (await kitchenAccessor.QueryOpenTicketLineOptionListAsync(tenantId, storeId, cancellationToken)).ToLookup(static x => x.LineId);
            return new(new KitchenTicketListResponse { Items = tickets.Select(x => OrderResponses.ToTicket(x, lines[x.Id], options)).ToList() });
        }

        var items = new List<KitchenTicketListResponseItem>();
        foreach (var ticket in (await kitchenAccessor.QueryDoneTicketListAsync(tenantId, storeId, DoneLimit * 4, cancellationToken)).Where(x => stations.Contains(x.StationId)).Take(DoneLimit))
        {
            var lines = await kitchenAccessor.QueryTicketLineListAsync(tenantId, ticket.Id, cancellationToken);
            var options = await kitchenAccessor.QueryTicketLineOptionListAsync(tenantId, ticket.Id, cancellationToken);
            items.Add(OrderResponses.ToTicket(ticket, lines, options.ToLookup(static x => x.LineId)));
        }

        return new(new KitchenTicketListResponse { Items = items });
    }

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    // 作り始め (Ordered から Cooking)
    public ValueTask<ServiceResult<KitchenTicketListResponseItem>> StartAsync(Guid ticketId, Guid lineId, CancellationToken cancellationToken) =>
        ChangeLineAsync(ticketId, lineId, OrderLineStatus.Cooking, cancellationToken);

    // できあがり (Ordered か Cooking から Ready)
    public ValueTask<ServiceResult<KitchenTicketListResponseItem>> ReadyAsync(Guid ticketId, Guid lineId, CancellationToken cancellationToken) =>
        ChangeLineAsync(ticketId, lineId, OrderLineStatus.Ready, cancellationToken);

    // 同じ操作の送り直し (すでにその状態) は変えずにチケットを返す
    private ValueTask<ServiceResult<KitchenTicketListResponseItem>> ChangeLineAsync(Guid ticketId, Guid lineId, OrderLineStatus status, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<KitchenTicketListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var ticket = await kitchenAccessor.QueryTicketAsync(tx, tenantId, storeId, ticketId, cancellationToken);
            if (ticket is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!context.StationIds.Contains(ticket.StationId))
            {
                return new(ServiceError.DeviceScope);
            }

            var line = await orderAccessor.QueryLineAsync(tx, tenantId, storeId, lineId, cancellationToken);
            if ((line is null) || (line.TicketId != ticketId))
            {
                return new(ServiceError.NotFound);
            }

            if (line.Status == status)
            {
                return new(await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken));
            }

            var changed = status == OrderLineStatus.Ready
                ? await orderAccessor.UpdateLineReadyAsync(tx, tenantId, lineId, context.Now, cancellationToken)
                : await orderAccessor.UpdateLineStartedAsync(tx, tenantId, lineId, context.Now, cancellationToken);
            if (changed == 0)
            {
                return new(new ServiceError(ErrorCodes.LineStatusInvalid));
            }

            var item = await AppendChangesAsync(transaction, ticket, true, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(item);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    // すべての明細をできあがりにして下げる (下げたチケットは変えずに返す)
    public ValueTask<ServiceResult<KitchenTicketListResponseItem>> BumpAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<KitchenTicketListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var ticket = await kitchenAccessor.QueryTicketAsync(tx, tenantId, storeId, ticketId, cancellationToken);
            if (ticket is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!context.StationIds.Contains(ticket.StationId))
            {
                return new(ServiceError.DeviceScope);
            }

            if (ticket.Status == KitchenTicketStatus.Done)
            {
                return new(await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken));
            }

            var changed = false;
            foreach (var line in await kitchenAccessor.QueryTicketLineListAsync(tx, tenantId, ticketId, cancellationToken))
            {
                if (line.Status is OrderLineStatus.Ordered or OrderLineStatus.Cooking)
                {
                    changed |= await orderAccessor.UpdateLineReadyAsync(tx, tenantId, line.Id, context.Now, cancellationToken) > 0;
                }
            }

            await kitchenAccessor.UpdateTicketDoneAsync(tx, tenantId, ticketId, context.Now, cancellationToken);
            var item = await AppendChangesAsync(transaction, ticket, changed, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(item);
        }, cancellationToken);
    }

    // 下げたチケットを戻す (押し間違い)。まだ出していない明細は作っている途中に戻す (戻したチケットは変えずに返す)
    public ValueTask<ServiceResult<KitchenTicketListResponseItem>> RecallAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return eventService.WriteAsync<ServiceResult<KitchenTicketListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var ticket = await kitchenAccessor.QueryTicketAsync(tx, tenantId, storeId, ticketId, cancellationToken);
            if (ticket is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!context.StationIds.Contains(ticket.StationId))
            {
                return new(ServiceError.DeviceScope);
            }

            if (ticket.Status == KitchenTicketStatus.Open)
            {
                return new(await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken));
            }

            var changed = false;
            foreach (var line in await kitchenAccessor.QueryTicketLineListAsync(tx, tenantId, ticketId, cancellationToken))
            {
                if (line.Status == OrderLineStatus.Ready)
                {
                    changed |= await orderAccessor.UpdateLineRecalledAsync(tx, tenantId, line.Id, cancellationToken) > 0;
                }
            }

            await kitchenAccessor.UpdateTicketReopenedAsync(tx, tenantId, ticketId, cancellationToken);
            var item = await AppendChangesAsync(transaction, ticket, changed, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(item);
        }, cancellationToken);
    }

    // 明細の状態が変わったら注文履歴と提供の一覧に、チケットはキッチンに知らせる
    private async ValueTask<KitchenTicketListResponseItem> AppendChangesAsync(StoreTransaction transaction, KitchenTicketEntity ticket, bool linesChanged, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (linesChanged)
        {
            var order = (await OrderResponses.LoadAsync(orderAccessor, transaction.Tx, transaction.TenantId, ticket.VisitId, cancellationToken)).First(x => x.Id == ticket.OrderId);
            var data = new OrderLinesUpdatedEventData
            {
                VisitId = ticket.VisitId,
                Orders = [order]
            };
            await transaction.AppendEventAsync(EventTypes.OrderLinesUpdated, data, [ticket.TableId], null, now, cancellationToken);
        }

        var item = await OrderResponses.LoadTicketAsync(kitchenAccessor, transaction.Tx, transaction.TenantId, transaction.StoreId, ticket.Id, cancellationToken);
        await transaction.AppendEventAsync(EventTypes.TicketUpdated, item, null, ticket.StationId, now, cancellationToken);
        return item;
    }
}
