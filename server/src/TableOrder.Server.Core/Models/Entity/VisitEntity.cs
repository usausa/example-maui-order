namespace TableOrder.Server.Core.Models.Entity;

// 来店。読み出すときは、テーブルの名前と注文の合計 (取消を除く) を足す
public sealed class VisitEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid TableId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public int Adults { get; set; }

    public int Children { get; set; }

    public VisitStatus Status { get; set; }

    public VisitOpenedBy OpenedBy { get; set; }

    public Guid? OpenedDeviceId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public VisitClosedBy? ClosedBy { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public string? ClosedStaffId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }

    // 読み出しで足す値
    public string TableName { get; set; } = default!;

    public decimal OrderTotal { get; set; }
}
