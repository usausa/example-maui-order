namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Orders;
using TableOrder.Server.Core.Accessors;

// 注文・チケットの応答を作る (注文履歴、通知、キッチン、提供で同じ形にする)
internal static class OrderResponses
{
    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 来店の注文 (通し番号の順)
    public static async ValueTask<List<OrderListResponseItem>> LoadAsync(OrderAccessor accessor, Guid tenantId, Guid visitId, CancellationToken cancellationToken)
    {
        var orders = await accessor.QueryListAsync(tenantId, visitId, cancellationToken);
        var lines = await accessor.QueryLineListAsync(tenantId, visitId, cancellationToken);
        var options = await accessor.QueryLineOptionListAsync(tenantId, visitId, cancellationToken);
        return ToOrders(orders, lines, options);
    }

    // 書き込みの中で読む (変えたあとの注文を応答と通知に入れる)
    public static async ValueTask<List<OrderListResponseItem>> LoadAsync(OrderAccessor accessor, DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken)
    {
        var orders = await accessor.QueryListAsync(tx, tenantId, visitId, cancellationToken);
        var lines = await accessor.QueryLineListAsync(tx, tenantId, visitId, cancellationToken);
        var options = await accessor.QueryLineOptionListAsync(tx, tenantId, visitId, cancellationToken);
        return ToOrders(orders, lines, options);
    }

    private static List<OrderListResponseItem> ToOrders(IEnumerable<OrderEntity> orders, IEnumerable<OrderLineEntity> lines, IEnumerable<OrderLineOptionEntity> options)
    {
        var optionsByLine = options.ToLookup(static x => x.LineId);
        var linesByOrder = lines.ToLookup(static x => x.OrderId);
        return orders.Select(order =>
        {
            var orderLines = linesByOrder[order.Id].ToList();
            return new OrderListResponseItem
            {
                Id = order.Id,
                VisitId = order.VisitId,
                OrderNo = order.OrderNo,
                Source = order.Source,
                OrderedAt = order.OrderedAt,
                Amount = orderLines.Where(static x => x.Status != OrderLineStatus.Cancelled).Sum(static x => x.Amount),
                Lines = orderLines.Select(x => ToLine(x, optionsByLine[x.Id])).ToList()
            };
        }).ToList();
    }

    private static OrderListResponseLine ToLine(OrderLineEntity line, IEnumerable<OrderLineOptionEntity> options) =>
        new()
        {
            Id = line.Id,
            ItemId = line.ItemId,
            Name = line.Name,
            Options = options.Select(static x => new OrderListResponseOption
            {
                OptionGroupId = x.OptionGroupId,
                OptionId = x.OptionId,
                Name = x.Name,
                PriceDelta = x.PriceDelta
            }).ToList(),
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            Amount = line.Amount,
            TaxRate = line.TaxRate,
            Timing = line.Timing,
            Status = line.Status,
            StationId = line.StationId,
            ServedAt = line.ServedAt,
            CancelledAt = line.CancelledAt,
            CancelReason = line.CancelReason
        };

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    // 書き込みの中で読む (変えたあとのチケットを応答と通知に入れる)
    public static async ValueTask<KitchenTicketListResponseItem> LoadTicketAsync(KitchenAccessor accessor, DbTransaction tx, Guid tenantId, Guid storeId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await accessor.QueryTicketAsync(tx, tenantId, storeId, ticketId, cancellationToken);
        var lines = await accessor.QueryTicketLineListAsync(tx, tenantId, ticketId, cancellationToken);
        var options = await accessor.QueryTicketLineOptionListAsync(tx, tenantId, ticketId, cancellationToken);
        return ToTicket(ticket!, lines, options.ToLookup(static x => x.LineId));
    }

    // キッチンの表示は日本語の名前だけにする
    public static KitchenTicketListResponseItem ToTicket(KitchenTicketEntity ticket, IEnumerable<OrderLineEntity> lines, ILookup<Guid, OrderLineOptionEntity> options) =>
        new()
        {
            Id = ticket.Id,
            StationId = ticket.StationId,
            OrderId = ticket.OrderId,
            VisitId = ticket.VisitId,
            TableName = ticket.TableName,
            OrderNo = ticket.OrderNo,
            CreatedAt = ticket.CreatedAt,
            Status = ticket.Status,
            DoneAt = ticket.DoneAt,
            Lines = lines.Select(x => new KitchenTicketListResponseLine
            {
                LineId = x.Id,
                Name = x.Name.Ja,
                Options = options[x.Id].Select(static o => o.Name.Ja).ToList(),
                Quantity = x.Quantity,
                Status = x.Status
            }).ToList()
        };
}
