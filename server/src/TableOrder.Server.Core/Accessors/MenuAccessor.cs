namespace TableOrder.Server.Core.Accessors;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class MenuAccessor
{
    // 店舗の今のメニュー (店舗の MenuPublicationId が指す公開)
    [QueryFirst]
    public partial ValueTask<MenuPublicationEntity?> QueryCurrentAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<MenuPublicationEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 新しい店舗のメニュー (外部の連携で公開するまでは、ほかの店舗のメニューかサンプルのメニューを写す)
    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, string menuVersion, string content, DateTimeOffset now, CancellationToken cancellationToken);
}
