namespace TableOrder.Server.Core.Accessors;

// 端末の登録の受け口 (ペアリングコードと登録トークンを出し、登録で使った数を数える)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DeviceEnrollmentAccessor
{
    // 店舗の登録トークン (新しいものから)
    [Query]
    public partial ValueTask<List<DeviceEnrollmentEntity>> QueryTokenListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(Guid tenantId, Guid id, Guid storeId, DeviceKind kind, EnrollmentMethod method, string? pairingCode, byte[]? tokenHash, Guid? tableId, string? stationIds, int maxUses, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken cancellationToken);

    // 使える (取り下げていない、期限の内、台数が残る) ときだけ数を足す。0 件なら使えない (同時の登録で台数を超えない)
    [Execute]
    public partial ValueTask<int> AddUsedCountAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 登録トークンを取り消す (取り消していないものだけ)
    [Execute]
    public partial ValueTask<int> UpdateRevokedAsync(Guid tenantId, Guid storeId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}
