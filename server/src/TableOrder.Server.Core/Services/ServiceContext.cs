namespace TableOrder.Server.Core.Services;

// 要求の文脈。テナント・店舗・端末は確かめたトークンから入れ、要求の中の値からは入れない
public sealed record ServiceContext(DateTimeOffset Now)
{
    public Guid? TenantId { get; init; }

    public Guid? StoreId { get; init; }

    public Guid? DeviceId { get; init; }

    public DeviceKind? DeviceKind { get; init; }

    public Guid? TableId { get; init; }

    public IReadOnlyList<Guid> StationIds { get; init; } = [];

    // テナントのない文脈 (端末の登録、トークンの要求、管理画面) から店舗の処理を呼んだら、絞らずに読む前に止める
    public Guid RequireTenantId() => TenantId ?? throw new InvalidOperationException("Tenant is not set in the service context.");

    public Guid RequireStoreId() => StoreId ?? throw new InvalidOperationException("Store is not set in the service context.");

    public Guid RequireDeviceId() => DeviceId ?? throw new InvalidOperationException("Device is not set in the service context.");
}
