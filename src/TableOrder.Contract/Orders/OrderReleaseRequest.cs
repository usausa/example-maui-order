namespace TableOrder.Contract.Orders;

// 食後の品をお願いする (空ならすべて)
public sealed class OrderReleaseRequest
{
    public IReadOnlyList<Guid> LineIds { get; set; } = default!;
}
