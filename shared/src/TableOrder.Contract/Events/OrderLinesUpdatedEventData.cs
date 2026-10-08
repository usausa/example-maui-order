namespace TableOrder.Contract.Events;

using TableOrder.Contract.Orders;

// order.lines.updated の中身 (明細の状態が変わった注文を、注文のすべての明細と一緒に送る)
public sealed class OrderLinesUpdatedEventData
{
    public Guid VisitId { get; set; }

    public IReadOnlyList<OrderListResponseItem> Orders { get; set; } = default!;
}
