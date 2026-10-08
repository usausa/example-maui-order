namespace TableOrder.Server.Core.Models.Entity;

// 店舗ごとの通知の通し番号
public sealed class EventSequenceEntity
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public long LastSeq { get; set; }
}
