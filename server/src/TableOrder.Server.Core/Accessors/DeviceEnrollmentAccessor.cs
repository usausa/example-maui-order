namespace TableOrder.Server.Core.Accessors;

// 端末の登録の受け口 (ペアリングコードと登録トークンを出し、登録で使った数を数える)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DeviceEnrollmentAccessor
{
    [Execute]
    public partial ValueTask<int> InsertAsync(Guid tenantId, Guid id, Guid storeId, DeviceKind kind, EnrollmentMethod method, string? pairingCode, byte[]? tokenHash, Guid? tableId, string? stationIds, int maxUses, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken cancellationToken);

    // 使える (取り下げていない、期限の内、台数が残る) ときだけ数を足す。0 件なら使えない (同時の登録で台数を超えない)
    [Execute]
    public partial ValueTask<int> AddUsedCountAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}
