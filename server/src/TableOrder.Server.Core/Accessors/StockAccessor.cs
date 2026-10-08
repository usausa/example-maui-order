namespace TableOrder.Server.Core.Accessors;

// 品切れと残りの数 (Available の品は行を持たない)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StockAccessor
{
    [Query]
    public partial ValueTask<List<StockEntity>> QueryListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 書き込みの中で読む (すべて戻すときに、戻した品を通知に入れる)
    [Query]
    public partial ValueTask<List<StockEntity>> QueryListAsync(DbTransaction tx, Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpsertAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid targetId, StockTargetKind targetKind, StockStatus status, int? remaining, DateTimeOffset now, CancellationToken cancellationToken);

    // 残りの数を足し引きする (注文で減らす)。残りがなくなったら SoldOut にし、残りより多くは引かない
    [Execute]
    public partial ValueTask<int> AddRemainingAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid targetId, int quantity, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid targetId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAllAsync(DbTransaction tx, Guid tenantId, Guid storeId, CancellationToken cancellationToken);
}
