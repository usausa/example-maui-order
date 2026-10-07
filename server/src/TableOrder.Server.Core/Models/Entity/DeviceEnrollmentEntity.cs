namespace TableOrder.Server.Core.Models.Entity;

// 端末の登録の受け口 (ペアリングコードと登録トークン)
public sealed class DeviceEnrollmentEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public DeviceKind Kind { get; set; }

    public EnrollmentMethod Method { get; set; }

    // 登録トークン (TokenHash) はハッシュで引くだけで読み出さないので、行の型に持たない
    public string? PairingCode { get; set; }

    public Guid? TableId { get; set; }

    // 持ち場の一覧 (JSON の配列)
    public string? StationIds { get; set; }

    public int MaxUses { get; set; }

    public int UsedCount { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
