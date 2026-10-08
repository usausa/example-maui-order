namespace TableOrder.Server.Core.Models.Entity;

// 明細。名前・価格・タグは注文したときのものを写す (メニューが変わっても履歴と会計を変えない)
public sealed class OrderLineEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid OrderId { get; set; }

    public Guid VisitId { get; set; }

    public int LineNo { get; set; }

    public Guid ItemId { get; set; }

    public string ItemCode { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    // 商品と選んだオプションのタグ (JSON の配列)
    public string Tags { get; set; } = default!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxRate { get; set; }

    public OrderTiming Timing { get; set; }

    public OrderLineStatus Status { get; set; }

    public Guid? StationId { get; set; }

    public ServedBy ServedBy { get; set; }

    public Guid? TicketId { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public DateTimeOffset? ServedAt { get; set; }

    public string? ServedStaffId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public string? CancelStaffId { get; set; }

    // 数量の一部を取り消して分けた元の明細
    public Guid? SplitFromLineId { get; set; }
}
