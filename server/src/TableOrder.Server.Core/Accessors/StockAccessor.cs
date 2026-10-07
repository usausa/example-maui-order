namespace TableOrder.Server.Core.Accessors;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StockAccessor
{
    [Query]
    public partial ValueTask<List<StockEntity>> QueryListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);
}
