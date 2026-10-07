namespace TableOrder.Contract.Menu;

// 売れるかどうか (Available でないものだけを返す)
public sealed class StockResponse
{
    public IReadOnlyList<StockResponseItem> Items { get; set; } = default!;
}

public sealed class StockResponseItem
{
    // 商品かオプションの Id
    public Guid TargetId { get; set; }

    public StockTargetKind TargetKind { get; set; }

    public StockStatus Status { get; set; }

    // 残りの数 (Limited のときだけ)
    public int? Remaining { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
