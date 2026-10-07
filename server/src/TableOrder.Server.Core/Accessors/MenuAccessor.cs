namespace TableOrder.Server.Core.Accessors;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class MenuAccessor
{
    // 店舗の今のメニュー (店舗の MenuPublicationId が指す公開)
    [QueryFirst]
    public partial ValueTask<MenuPublicationEntity?> QueryCurrentAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);
}
