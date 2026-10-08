namespace TableOrder.Contract.Orders;

// 来店の注文 (注文履歴と明細の状態)
public sealed class OrderListResponse
{
    public IReadOnlyList<OrderListResponseItem> Items { get; set; } = default!;
}

public sealed class OrderListResponseItem
{
    public Guid Id { get; set; }

    public Guid VisitId { get; set; }

    // 来店の中の通し番号
    public int OrderNo { get; set; }

    public OrderSource Source { get; set; }

    public DateTimeOffset OrderedAt { get; set; }

    // 明細の合計 (取消を除く)
    public decimal Amount { get; set; }

    public IReadOnlyList<OrderListResponseLine> Lines { get; set; } = default!;
}

public sealed class OrderListResponseLine
{
    public Guid Id { get; set; }

    public Guid ItemId { get; set; }

    // 注文したときの名前 (メニューが変わっても履歴の表示を変えない)
    public LocalizedText Name { get; set; } = default!;

    public IReadOnlyList<OrderListResponseOption> Options { get; set; } = default!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxRate { get; set; }

    public OrderTiming Timing { get; set; }

    public OrderLineStatus Status { get; set; }

    // 作る持ち場 (作らない品は null)
    public Guid? StationId { get; set; }

    public DateTimeOffset? ServedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }
}

public sealed class OrderListResponseOption
{
    public Guid OptionGroupId { get; set; }

    public Guid OptionId { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public decimal PriceDelta { get; set; }
}
