namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 扱えるテナント (運営者) か店舗 (テナントの利用者) の一覧
public sealed partial class DashboardPage
{
    private List<TenantEntity> tenants = [];

    private List<StoreEntity> stores = [];

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required AdminScope Scope { get; set; }

    [Inject]
    public required TenantService TenantService { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        if (Scope.IsOperator)
        {
            tenants = await TenantService.GetAllAsync(CancellationToken.None);
        }
        else if (Scope.TenantId is { } tenantId)
        {
            stores = (await TenantService.GetStoreAllAsync(tenantId, CancellationToken.None))
                .Where(x => Scope.CanUseStore(tenantId, x.Id))
                .ToList();
        }
    }
}
