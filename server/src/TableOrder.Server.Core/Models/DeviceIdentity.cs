namespace TableOrder.Server.Core.Models;

// アクセストークンに入れる端末の文脈 (テナント、店舗、種類、置き場所)
public sealed record DeviceIdentity(
    Guid TenantId,
    Guid StoreId,
    Guid DeviceId,
    DeviceKind Kind,
    Guid? TableId,
    IReadOnlyList<Guid> StationIds);
