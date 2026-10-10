namespace TableOrder.Server.Core.Accessors;

// チェーンと店舗の設定 (管理画面で替え、端末の設定に入れる)。チェーンの設定はテナントの行に持つ
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class SettingsAccessor
{
    [QueryFirst]
    public partial ValueTask<BrandEntity?> QueryBrandAsync(Guid tenantId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateBrandAsync(Guid tenantId, LocalizedText brandName, string? logoImageName, string? theme, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // 店舗の設定 (版で確かめ、設定の版を上げる)。PIN のハッシュは替えるときだけ渡す
    [Execute]
    public partial ValueTask<int> UpdateStoreAsync(DbTransaction tx, Guid tenantId, Guid storeId, string languages, string paymentMethods, bool electronicReceipt, string features, string? staffPinHash, int version, DateTimeOffset now, CancellationToken cancellationToken);

    // チェーンの設定を替えたときに、店舗の設定の版を上げる
    [Execute]
    public partial ValueTask<int> UpdateSettingsVersionAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateTimeOffset now, CancellationToken cancellationToken);

    // 使わなくしたものも含めた呼び出しの用件 (表示順)
    [Query]
    public partial ValueTask<List<CallReasonEntity>> QueryCallReasonAllAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateCallReasonAsync(DbTransaction tx, Guid tenantId, Guid storeId, string code, bool isActive, CancellationToken cancellationToken);
}
