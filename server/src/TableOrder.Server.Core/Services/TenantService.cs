namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// テナントをまたぐ処理 (運営者の管理画面だけが使う)
public sealed class TenantService
{
    private readonly TenantAccessor tenantAccessor;

    public TenantService(TenantAccessor tenantAccessor)
    {
        this.tenantAccessor = tenantAccessor;
    }

    public ValueTask<List<TenantEntity>> GetAllAsync(CancellationToken cancellationToken) =>
        tenantAccessor.QueryAllAsync(cancellationToken);
}
