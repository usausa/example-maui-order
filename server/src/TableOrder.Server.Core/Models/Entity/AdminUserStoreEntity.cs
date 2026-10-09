namespace TableOrder.Server.Core.Models.Entity;

// 店舗の担当が受け持つ店舗
public sealed class AdminUserStoreEntity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid StoreId { get; set; }
}
