namespace TableOrder.Server.Core.Models.Entity;

// 裏の処理で扱う店舗
public sealed class StoreKeyEntity
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }
}
