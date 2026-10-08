namespace TableOrder.Server.Core.Accessors;

// 店舗と、店舗に付く設定 (呼び出しの用件、テーブル)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StoreAccessor
{
    [QueryFirst]
    public partial ValueTask<StoreEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    // テナントの店舗 (管理画面で店舗を選ぶ)
    [Query]
    public partial ValueTask<List<StoreEntity>> QueryAllAsync(Guid tenantId, CancellationToken cancellationToken);

    // 書き込みの中で読む (変えたあとの店舗を通知に入れる)
    [QueryFirst]
    public partial ValueTask<StoreEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 注文の一時停止と再開
    [Execute]
    public partial ValueTask<int> UpdateOrderingAsync(DbTransaction tx, Guid tenantId, Guid id, bool paused, LocalizedText? message, DateTimeOffset now, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CallReasonEntity>> QueryCallReasonListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<DiningTableEntity?> QueryTableAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 店舗の使っているテーブル (ペアリングコードを出すときに、置き場所のテーブルを確かめる)
    [QueryFirst]
    public partial ValueTask<DiningTableEntity?> QueryActiveTableAsync(Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 書き込みの中で、来店や端末を置くテーブルを確かめる
    [QueryFirst]
    public partial ValueTask<DiningTableEntity?> QueryActiveTableAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 書き込みの中で、人数の入る空席のうち定員の小さいテーブル (同じなら表示順) を選ぶ (受付機の来店の開始)
    [QueryFirst]
    public partial ValueTask<DiningTableEntity?> QueryVacantTableByGuestsAsync(DbTransaction tx, Guid tenantId, Guid storeId, int guests, CancellationToken cancellationToken);

    // 使っているテーブルと今の来店の要約 (表示順)
    [Query]
    public partial ValueTask<List<TableSummaryEntity>> QueryTableSummaryListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);
}
