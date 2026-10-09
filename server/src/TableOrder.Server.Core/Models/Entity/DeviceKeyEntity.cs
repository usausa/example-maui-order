namespace TableOrder.Server.Core.Models.Entity;

// 裏の処理で扱う端末
public sealed class DeviceKeyEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }
}
