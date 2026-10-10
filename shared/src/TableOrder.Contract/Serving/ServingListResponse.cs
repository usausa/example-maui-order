namespace TableOrder.Contract.Serving;

// 提供を待つ明細 (ホール端末)。テーブルごとに、できあがりの古い順
// 払い終えて閉じた来店の品も、その営業日のうちは出す (VisitStatus が Closed。同じテーブルの次のお客様の品と見分ける)
public sealed class ServingListResponse
{
    public IReadOnlyList<ServingListResponseItem> Items { get; set; } = default!;
}

public sealed class ServingListResponseItem
{
    public Guid VisitId { get; set; }

    public Guid TableId { get; set; }

    public string TableName { get; set; } = default!;

    public VisitStatus VisitStatus { get; set; }

    public IReadOnlyList<ServingListResponseLine> Lines { get; set; } = default!;
}

public sealed class ServingListResponseLine
{
    public Guid LineId { get; set; }

    public Guid OrderId { get; set; }

    public int OrderNo { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public IReadOnlyList<LocalizedText> Options { get; set; } = default!;

    public int Quantity { get; set; }

    public OrderLineStatus Status { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }
}
