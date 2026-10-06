namespace TableOrder.Domain.Enums;

// 明細の調理と提供の段階 (Held は食後にお願いされるまで止めている)
public enum OrderLineStatus
{
    Held,
    Ordered,
    Cooking,
    Ready,
    Served,
    Cancelled
}
