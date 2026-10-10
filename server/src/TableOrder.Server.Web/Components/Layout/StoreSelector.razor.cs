namespace TableOrder.Server.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 管理画面で扱うテナントと店舗を選ぶ (選んだものは回線の間だけ覚える)
// 運営者はテナントを選び、テナントの利用者は自分のテナントの店舗 (店舗の担当は受け持つ店舗) から選ぶ
public sealed partial class StoreSelector : IDisposable
{
    private List<TenantEntity> tenants = [];

    private List<StoreEntity> stores = [];

    [Inject]
    public required AdminScope Scope { get; set; }

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required TenantService TenantService { get; set; }

    private string TenantName => tenants.FirstOrDefault()?.Name ?? string.Empty;

    // テナントの利用者は自分のテナントを選んでおき、選べる店舗が 1 つだけならその店舗も選んでおく
    protected override async Task OnInitializedAsync()
    {
        Selection.ChoicesChanged += OnChoicesChanged;
        await LoadAsync();

        var tenantId = Selection.TenantId ?? (Scope.IsOperator ? null : Scope.TenantId);
        if (tenantId is { } id)
        {
            stores = await LoadStoresAsync(id);
            if (Selection.TenantId is null)
            {
                Selection.Select(id, stores.Count == 1 ? stores[0].Id : null);
            }
        }
    }

    public void Dispose() => Selection.ChoicesChanged -= OnChoicesChanged;

    // テナントや店舗を足したら一覧を読み直す (画面の操作の中から呼ばれるので、文脈を始め直す)
    private void OnChoicesChanged(object? sender, EventArgs e) =>
        _ = ReloadAsync(async () =>
        {
            await LoadAsync();
            stores = Selection.TenantId is { } id ? await LoadStoresAsync(id) : [];
        });

    private async Task LoadAsync()
    {
        if (Scope.IsOperator)
        {
            tenants = await TenantService.GetAllAsync(CancellationToken.None);
        }
        else if (Scope.TenantId is { } own)
        {
            tenants = await TenantService.GetAsync(own, CancellationToken.None) is { } tenant ? [tenant] : [];
        }
    }

    private async Task SelectTenantAsync(Guid? tenantId)
    {
        stores = tenantId is { } id ? await LoadStoresAsync(id) : [];
        Selection.Select(tenantId, stores.Count == 1 ? stores[0].Id : null);
    }

    private void SelectStore(Guid? storeId) => Selection.Select(Selection.TenantId, storeId);

    private async Task<List<StoreEntity>> LoadStoresAsync(Guid tenantId) =>
        (await TenantService.GetStoreAllAsync(tenantId, CancellationToken.None))
            .Where(x => Scope.CanUseStore(tenantId, x.Id))
            .ToList();

    // 使わなくした店舗は、そうとわかるように出す
    private static string StoreName(StoreEntity store) =>
        store.IsActive ? $"{store.Name.Ja} ({store.Code})" : $"{store.Name.Ja} ({store.Code}。使っていない)";
}
