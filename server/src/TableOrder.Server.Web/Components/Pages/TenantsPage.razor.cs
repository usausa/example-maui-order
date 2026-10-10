namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Server.Web.Application.Context;

// テナントの管理 (一覧、登録、止める・戻す)。運営者だけが開ける
public sealed partial class TenantsPage
{
    private List<TenantSummaryEntity> tenants = [];

    private string addCode = string.Empty;

    private string addName = string.Empty;

    private string addBrandJa = string.Empty;

    private string addBrandEn = string.Empty;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required TenantService TenantService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        tenants = await TenantService.GetSummaryListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Tenant
    //--------------------------------------------------------------------------------

    private Task AddAsync() => RunOnceAsync(AddCoreAsync);

    private async Task AddCoreAsync()
    {
        var result = await TenantService.CreateAsync(addCode, addName, new LocalizedText { Ja = addBrandJa, En = addBrandEn }, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(AdminNames.ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"{result.Value.Name} を登録しました", Severity.Success);
        addCode = string.Empty;
        addName = string.Empty;
        addBrandJa = string.Empty;
        addBrandEn = string.Empty;
        await LoadAsync();
        Selection.NotifyChoicesChanged();
    }

    // 止めると、テナントの端末はトークンを断られて止まり、利用者はサインインできなくなる
    private async Task SetSuspendedAsync(TenantSummaryEntity tenant, bool suspended)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            suspended ? "テナントを止める" : "テナントを戻す",
            suspended
                ? $"{tenant.Name} を止めます。テナントの端末は止まり、利用者はサインインできなくなります。"
                : $"{tenant.Name} を戻します。端末と利用者は、また使えるようになります。",
            yesText: suspended ? "止める" : "戻す",
            cancelText: "やめる");
        if (confirmed != true)
        {
            return;
        }

        var error = await TenantService.SetSuspendedAsync(tenant.Id, suspended, tenant.Version, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add(suspended ? $"{tenant.Name} を止めました" : $"{tenant.Name} を戻しました", Severity.Success);
        await LoadAsync();
    }
}
