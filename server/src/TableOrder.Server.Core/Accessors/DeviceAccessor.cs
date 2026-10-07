namespace TableOrder.Server.Core.Accessors;

// 端末と、端末の持ち場・状態
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DeviceAccessor
{
    [QueryFirst]
    public partial ValueTask<DeviceEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<DeviceStationEntity>> QueryStationListAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, DeviceKind kind, string name, Guid? tableId, string publicKey, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertStationAsync(DbTransaction tx, Guid tenantId, Guid deviceId, Guid stationId, CancellationToken cancellationToken);

    // 状態の報告。アプリの版は送られたときだけ替える
    [Execute]
    public partial ValueTask<int> UpsertStatusAsync(Guid tenantId, Guid deviceId, string? appVersion, decimal? batteryLevel, bool? isCharging, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);
}
