namespace TableOrder.Server.Core.Accessors;

// 店舗の通知と、その通し番号
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class EventAccessor
{
    // 通し番号の行を作るか触る (トランザクションの最初に呼び、店舗の書き込みを 1 つずつにするロックをとる)
    [Execute]
    public partial ValueTask<int> UpsertSequenceAsync(DbTransaction tx, Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 通し番号を 1 つ進めて返す
    [ExecuteScalar]
    public partial ValueTask<long> AddSeqAsync(DbTransaction tx, Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid storeId, long seq, string type, DateTimeOffset occurredAt, string data, string? tableIds, Guid? stationId, CancellationToken cancellationToken);

    // 店舗の今の通し番号 (通知を書いたことがなければ 0)
    [ExecuteScalar]
    public partial ValueTask<long> QueryLastSeqAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 残している最も古い通し番号 (残していなければ 0)
    [ExecuteScalar]
    public partial ValueTask<long> QueryFirstSeqAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // after より後の通知 (seq の順)
    [Query]
    public partial ValueTask<List<EventEntity>> QueryListAsync(Guid tenantId, Guid storeId, long after, int limit, CancellationToken cancellationToken);
}
