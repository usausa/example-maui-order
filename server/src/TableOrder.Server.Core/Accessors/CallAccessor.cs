namespace TableOrder.Server.Core.Accessors;

// 呼び出し
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class CallAccessor
{
    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid visitId, string reasonCode, Guid? deviceId, DateTimeOffset now, CancellationToken cancellationToken);

    // 店舗の呼び出し (来店の今のテーブルを足す)
    [QueryFirst]
    public partial ValueTask<CallEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 来店の同じ用件の終わっていない呼び出し
    [QueryFirst]
    public partial ValueTask<CallEntity?> QueryOpenByReasonAsync(DbTransaction tx, Guid tenantId, Guid visitId, string reasonCode, CancellationToken cancellationToken);

    // 来店の呼び出し (古い順)
    [Query]
    public partial ValueTask<List<CallEntity>> QueryListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 店舗の終わっていない呼び出し (古い順)
    [Query]
    public partial ValueTask<List<CallEntity>> QueryOpenListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 店舗の終わった呼び出し (終わった新しい順に limit 件)
    [Query]
    public partial ValueTask<List<CallEntity>> QueryDoneListAsync(Guid tenantId, Guid storeId, int limit, CancellationToken cancellationToken);

    // 向かう (Open から)
    [Execute]
    public partial ValueTask<int> UpdateAcknowledgedAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 対応した (Open か Acknowledged から)
    [Execute]
    public partial ValueTask<int> UpdateDoneAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}
