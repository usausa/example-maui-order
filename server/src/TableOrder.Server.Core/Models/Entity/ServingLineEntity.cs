namespace TableOrder.Server.Core.Models.Entity;

// 提供を待つ明細と、そのテーブルと来店の状態 (ホールの提供の一覧)
public sealed class ServingLineEntity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid VisitId { get; set; }

    public int OrderNo { get; set; }

    public Guid TableId { get; set; }

    public string TableName { get; set; } = default!;

    public VisitStatus VisitStatus { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public int Quantity { get; set; }

    public OrderLineStatus Status { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }
}
