namespace TableOrder.Server.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 管理画面で扱うテナントと店舗を選ぶ (選んだものは回線の間だけ覚える)
public sealed partial class StoreSelector
{
    private List<TenantEntity> tenants = [];

    private List<StoreEntity> stores = [];

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required TenantService TenantService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        tenants = await TenantService.GetAllAsync(CancellationToken.None);
        if (Selection.TenantId is { } tenantId)
        {
            stores = await TenantService.GetStoreAllAsync(tenantId, CancellationToken.None);
        }
    }

    // 店舗が 1 つだけのテナントは、その店舗を選んでおく
    private async Task SelectTenantAsync(Guid? tenantId)
    {
        stores = tenantId is { } id ? await TenantService.GetStoreAllAsync(id, CancellationToken.None) : [];
        Selection.Select(tenantId, stores.Count == 1 ? stores[0].Id : null);
    }

    private void SelectStore(Guid? storeId) => Selection.Select(Selection.TenantId, storeId);

    private static string StoreName(StoreEntity store) => $"{store.Name.Ja} ({store.Code})";
}
