namespace TableOrder.Server.Core.Accessors;

// 店舗と、店舗に付く設定 (呼び出しの用件、テーブル)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StoreAccessor
{
    [QueryFirst]
    public partial ValueTask<StoreEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CallReasonEntity>> QueryCallReasonListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<DiningTableEntity?> QueryTableAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}
