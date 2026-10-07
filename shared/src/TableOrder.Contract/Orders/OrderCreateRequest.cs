namespace TableOrder.Contract.Orders;

// 注文の送信。Id と明細の Id は端末が採番し、送り直すときも同じ Id を使う
public sealed class OrderCreateRequest
{
    public Guid Id { get; set; }

    // 表示していたメニュー (価格が変わっていれば受け付けない)
    public string MenuVersion { get; set; } = default!;

    public IReadOnlyList<OrderCreateRequestLine> Lines { get; set; } = default!;
}

public sealed class OrderCreateRequestLine
{
    public Guid Id { get; set; }

    public Guid ItemId { get; set; }

    public IReadOnlyList<Guid> OptionIds { get; set; } = default!;

    public int Quantity { get; set; }

    // 端末が表示した単価 (サーバが計算し直して確かめる)
    public decimal UnitPrice { get; set; }

    public OrderTiming Timing { get; set; }
}
