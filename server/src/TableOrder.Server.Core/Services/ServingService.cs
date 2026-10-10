namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Contract.Serving;
using TableOrder.Server.Core.Accessors;

public sealed class ServingService
{
    // 一度に提供にできる明細の数
    private const int MaxServeLines = 100;

    private readonly ServiceContextProvider contextProvider;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly KitchenAccessor kitchenAccessor;

    private readonly EventService eventService;

    public ServingService(
        ServiceContextProvider contextProvider,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        KitchenAccessor kitchenAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.kitchenAccessor = kitchenAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // 提供を待つ明細をテーブルごとに (できあがりの古い順)。状態の既定は Ready
    public async ValueTask<ServiceResult<ServingListResponse>> GetListAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = OrderLineStatus.Ready;
        if (!String.IsNullOrEmpty(status) &&
            (!Enum.TryParse(status, true, out filter) || (filter is not (OrderLineStatus.Ordered or OrderLineStatus.Cooking or OrderLineStatus.Ready))))
        {
            return new(ServiceError.Validation("status", "Ordered / Cooking / Ready のどれかを送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var lines = await orderAccessor.QueryServingLineListAsync(tenantId, storeId, filter, cancellationToken);
        var options = (await orderAccessor.QueryServingLineOptionListAsync(tenantId, storeId, filter, cancellationToken)).ToLookup(static x => x.LineId);
        return new(new ServingListResponse
        {
            Items = lines
                .GroupBy(static x => x.VisitId)
                .Select(g => new ServingListResponseItem
                {
                    VisitId = g.Key,
                    TableId = g.First().TableId,
                    TableName = g.First().TableName,
                    Lines = g.Select(x => new ServingListResponseLine
                    {
                        LineId = x.Id,
                        OrderId = x.OrderId,
                        OrderNo = x.OrderNo,
                        Name = x.Name,
                        Options = options[x.Id].Select(static o => o.Name).ToList(),
                        Quantity = x.Quantity,
                        Status = x.Status,
                        ReadyAt = x.ReadyAt
                    }).ToList()
                })
                .ToList()
        });
    }

    //--------------------------------------------------------------------------------
    // Serve
    //--------------------------------------------------------------------------------

    // 提供した (できあがりの前の品も出せる)。出した明細は送り直しても変えない
    public async ValueTask<ServiceError?> ServeAsync(ServeRequest request, CancellationToken cancellationToken)
    {
        var lineIds = RequestValues.ListOf(request.LineIds).Distinct().ToList();
        if ((lineIds.Count == 0) || (lineIds.Count > MaxServeLines))
        {
            return ServiceError.Validation("lineIds", $"明細の Id を 1 つから {MaxServeLines} まで送ってください");
        }

        if (request.StaffId?.Length > Length.StaffId)
        {
            return ServiceError.Validation("staffId", $"スタッフの Id を {Length.StaffId} 文字までで送ってください");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = context.Now;
        return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var visits = new Dictionary<Guid, VisitEntity>();
            var orders = new HashSet<Guid>();
            var tickets = new HashSet<Guid>();
            foreach (var lineId in lineIds)
            {
                var line = await orderAccessor.QueryLineAsync(tx, tenantId, storeId, lineId, cancellationToken);
                if (line is null)
                {
                    return ServiceError.NotFound;
                }

                if (line.Status == OrderLineStatus.Served)
                {
                    continue;
                }

                if (line.Status is OrderLineStatus.Held or OrderLineStatus.Cancelled)
                {
                    return new ServiceError(ErrorCodes.LineStatusInvalid);
                }

                if (!visits.TryGetValue(line.VisitId, out var visit))
                {
                    visit = (await visitAccessor.QueryAsync(tx, tenantId, storeId, line.VisitId, cancellationToken))!;
                    if (visit.Status is not (VisitStatus.Open or VisitStatus.Paying))
                    {
                        return new ServiceError(ErrorCodes.VisitNotOpen);
                    }

                    visits[line.VisitId] = visit;
                }

                if (await orderAccessor.UpdateLineServedAsync(tx, tenantId, lineId, request.StaffId, now, cancellationToken) == 0)
                {
                    return new ServiceError(ErrorCodes.LineStatusInvalid);
                }

                orders.Add(line.OrderId);
                if (line.TicketId is { } ticketId)
                {
                    tickets.Add(ticketId);
                }
            }

            if (orders.Count == 0)
            {
                return null;
            }

            foreach (var visit in visits.Values)
            {
                var changed = (await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visit.Id, cancellationToken)).Where(x => orders.Contains(x.Id)).ToList();
                if (changed.Count > 0)
                {
                    var data = new OrderLinesUpdatedEventData
                    {
                        VisitId = visit.Id,
                        Orders = changed
                    };
                    await transaction.AppendEventAsync(EventTypes.OrderLinesUpdated, data, [visit.TableId], null, now, cancellationToken);
                }
            }

            foreach (var ticketId in tickets)
            {
                var ticket = await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.TicketUpdated, ticket, null, ticket.StationId, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }
}
