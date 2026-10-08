namespace TableOrder.Server.Core.Accessors;

// 端末と、端末の持ち場・状態
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DeviceAccessor
{
    [QueryFirst]
    public partial ValueTask<DeviceEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 書き込みの中で読む (管理画面で替える端末の種類と店舗を確かめる)
    [QueryFirst]
    public partial ValueTask<DeviceEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 店舗の端末と、置き場所のテーブルの名前と状態の報告 (管理画面の一覧)
    [Query]
    public partial ValueTask<List<DeviceSummaryEntity>> QuerySummaryListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<DeviceStationEntity>> QueryStationListAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken);

    // 店舗の端末の持ち場 (管理画面の一覧)
    [Query]
    public partial ValueTask<List<DeviceStationEntity>> QueryStationListByStoreAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, DeviceKind kind, string name, Guid? tableId, string publicKey, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertStationAsync(DbTransaction tx, Guid tenantId, Guid deviceId, Guid stationId, CancellationToken cancellationToken);

    // 名前と置き場所の変更 (有効な端末を、表示していた版で替える)
    [Execute]
    public partial ValueTask<int> UpdateAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, string name, Guid? tableId, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 無効にする (次のトークンを出さない)
    [Execute]
    public partial ValueTask<int> UpdateRevokedAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 持ち場を替えるときに、前の持ち場を消す
    [Execute]
    public partial ValueTask<int> DeleteStationAsync(DbTransaction tx, Guid tenantId, Guid deviceId, CancellationToken cancellationToken);

    // 状態の報告。アプリの版は送られたときだけ替える
    [Execute]
    public partial ValueTask<int> UpsertStatusAsync(Guid tenantId, Guid deviceId, string? appVersion, decimal? batteryLevel, bool? isCharging, DateTimeOffset lastSeenAt, CancellationToken cancellationToken);
}
