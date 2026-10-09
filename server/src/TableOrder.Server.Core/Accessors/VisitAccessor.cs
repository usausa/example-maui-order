namespace TableOrder.Server.Core.Accessors;

// 来店と、確認のルールに答えた記録
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class VisitAccessor
{
    // 店舗の来店 (テーブルの名前と注文の合計を足す)
    [QueryFirst]
    public partial ValueTask<VisitEntity?> QueryAsync(Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 書き込みの中で読む (まだコミットしていない変更を見る)
    [QueryFirst]
    public partial ValueTask<VisitEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // テーブルの開いている来店 (Open か Paying)
    [QueryFirst]
    public partial ValueTask<VisitEntity?> QueryOpenByTableAsync(Guid tenantId, Guid storeId, Guid tableId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<VisitEntity?> QueryOpenByTableAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid tableId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid tableId, DateOnly businessDate, int adults, int children, VisitOpenedBy openedBy, Guid? openedDeviceId, DateTimeOffset now, CancellationToken cancellationToken);

    // 人数の変更 (開いている来店の、表示していた版だけ)
    [Execute]
    public partial ValueTask<int> UpdateGuestsAsync(DbTransaction tx, Guid tenantId, Guid id, int adults, int children, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // テーブルの移動 (Open の来店の、表示していた版だけ)
    [Execute]
    public partial ValueTask<int> UpdateTableAsync(DbTransaction tx, Guid tenantId, Guid id, Guid tableId, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 来店を終える (開いている来店の、表示していた版だけ)
    [Execute]
    public partial ValueTask<int> UpdateClosedAsync(DbTransaction tx, Guid tenantId, Guid id, VisitClosedBy closedBy, string? staffId, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 会計を始める (Open の来店の、表示していた版だけ)
    [Execute]
    public partial ValueTask<int> UpdatePayingAsync(DbTransaction tx, Guid tenantId, Guid id, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 会計をやめる (Paying から)
    [Execute]
    public partial ValueTask<int> UpdateReopenedAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 来店を取りやめる (Open の来店の、表示していた版だけ。ホールが終えたものとして記録する)
    [Execute]
    public partial ValueTask<int> UpdateCancelledAsync(DbTransaction tx, Guid tenantId, Guid id, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 来店の明細の数 (取消を除く)
    [ExecuteScalar]
    public partial ValueTask<long> CountOrderLineAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<VisitConfirmationEntity>> QueryConfirmationListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 書き込みの中で読む (足したばかりの記録を見る)
    [Query]
    public partial ValueTask<List<VisitConfirmationEntity>> QueryConfirmationListAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 答えた記録を足す (同じルールに答えていれば足さずに 0 件)
    [Execute]
    public partial ValueTask<int> InsertConfirmationAsync(DbTransaction tx, Guid tenantId, Guid visitId, Guid ruleId, Guid? deviceId, DateTimeOffset now, CancellationToken cancellationToken);
}
