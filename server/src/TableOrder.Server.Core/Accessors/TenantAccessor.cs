namespace TableOrder.Server.Core.Accessors;

// テナントの表 (この表だけは TenantId を持たないので、テナントの条件を調べるテストから外す)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TenantAccessor
{
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<TenantEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TenantEntity>> QueryAllAsync(CancellationToken cancellationToken);

    // 運営者の画面の一覧 (使っている店舗の数を添える)
    [Query]
    public partial ValueTask<List<TenantSummaryEntity>> QuerySummaryAllAsync(CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(Guid id, string code, string name, LocalizedText brandName, DateTimeOffset now, CancellationToken cancellationToken);

    // 止める・戻す (解約したテナントは替えない。読んだときの版のときだけ)
    [Execute]
    public partial ValueTask<int> UpdateStatusAsync(DbTransaction tx, Guid id, TenantStatus status, DateTimeOffset? suspendedAt, DateTimeOffset now, int version, CancellationToken cancellationToken);
}
