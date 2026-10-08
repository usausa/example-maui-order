namespace TableOrder.Server.Core.Models.Entity;

// 注文 (1 回の「注文を確定する」で送った明細の束)
public sealed class OrderEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid VisitId { get; set; }

    // 来店の中の通し番号
    public int OrderNo { get; set; }

    public OrderSource Source { get; set; }

    public Guid? DeviceId { get; set; }

    public string? StaffId { get; set; }

    public string MenuVersion { get; set; } = default!;

    public DateTimeOffset OrderedAt { get; set; }
}
