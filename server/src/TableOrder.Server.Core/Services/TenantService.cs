namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// テナントをまたぐ処理 (運営者の管理画面だけが使う)
public sealed class TenantService
{
    private readonly TenantAccessor tenantAccessor;

    private readonly StoreAccessor storeAccessor;

    public TenantService(
        TenantAccessor tenantAccessor,
        StoreAccessor storeAccessor)
    {
        this.tenantAccessor = tenantAccessor;
        this.storeAccessor = storeAccessor;
    }

    public ValueTask<List<TenantEntity>> GetAllAsync(CancellationToken cancellationToken) =>
        tenantAccessor.QueryAllAsync(cancellationToken);

    // 運営者が選んだテナントの店舗 (管理画面で店舗を選ぶ)
    public ValueTask<List<StoreEntity>> GetStoreAllAsync(Guid tenantId, CancellationToken cancellationToken) =>
        storeAccessor.QueryAllAsync(tenantId, cancellationToken);
}
