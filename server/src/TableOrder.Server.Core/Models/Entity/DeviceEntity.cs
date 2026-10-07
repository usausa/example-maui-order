namespace TableOrder.Server.Core.Models.Entity;

public sealed class DeviceEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public DeviceKind Kind { get; set; }

    public string Name { get; set; } = default!;

    public Guid? TableId { get; set; }

    // 端末の公開鍵 (JWK の JSON)
    public string PublicKey { get; set; } = default!;

    public bool IsActive { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
