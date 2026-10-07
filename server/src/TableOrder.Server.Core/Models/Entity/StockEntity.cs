namespace TableOrder.Server.Core.Models.Entity;

// 品切れと残りの数 (Available に戻したら行を消す)
public sealed class StockEntity
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public Guid TargetId { get; set; }

    public StockTargetKind TargetKind { get; set; }

    public StockStatus Status { get; set; }

    public int? Remaining { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
