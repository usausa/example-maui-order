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
}
