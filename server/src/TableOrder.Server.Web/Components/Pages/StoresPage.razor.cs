namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Components.Parts;

// 店舗の管理 (選んだテナントの店舗の一覧、追加、基本の変更、使わなくする)。運営者とテナントの管理者だけが開ける
public sealed partial class StoresPage : IDisposable
{
    private List<StoreEntity> stores = [];

    private StoreForm addForm = new();

    private StoreEntity? editing;

    private StoreForm editForm = new();

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required StoreSetupService StoreSetupService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync()
    {
        Selection.Changed += OnSelectionChanged;
        return LoadAsync();
    }

    public void Dispose() => Selection.Changed -= OnSelectionChanged;

    // テナントを選び直したら読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(async () =>
        {
            using (BeginServiceScope())
            {
                await LoadAsync();
            }

            StateHasChanged();
        });

    private async Task LoadAsync()
    {
        editing = null;
        stores = Selection.TenantId is null ? [] : await StoreSetupService.GetListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // 写す店舗がなければ、サンプルのメニューを入れる (外部の連携でメニューを公開するまでの仮)
    private async Task AddAsync()
    {
        var sampleMenu = await DatabaseService.ReadSampleMenuAsync(AssetPaths.SampleMenu, CancellationToken.None);
        var result = await StoreSetupService.AddAsync(addForm.ToBasics(), addForm.StaffPin, addForm.SourceStoreId, sampleMenu, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(AdminNames.ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"{result.Value.Name.Ja} を足しました", Severity.Success);
        addForm = new StoreForm();
        await LoadAsync();
        Selection.NotifyChoicesChanged();
    }

    private void Edit(StoreEntity store)
    {
        editing = store;
        editForm = StoreForm.From(store);
    }

    private void CancelEdit() => editing = null;

    private async Task SaveAsync()
    {
        if (editing is not { } store)
        {
            return;
        }

        var error = await StoreSetupService.UpdateAsync(store.Id, editForm.ToBasics(), store.Version, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{editForm.NameJa.Trim()} を替えました。店舗の端末は起動からやり直します", Severity.Success);
        await LoadAsync();
        Selection.NotifyChoicesChanged();
    }

    private async Task SetActiveAsync(bool isActive)
    {
        if (editing is not { } store)
        {
            return;
        }

        if (!isActive)
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                "店舗を使わなくする",
                $"{store.Name.Ja} を使わなくします。新しい端末は登録できなくなります。",
                yesText: "使わなくする",
                cancelText: "やめる");
            if (confirmed != true)
            {
                return;
            }
        }

        var error = await StoreSetupService.SetActiveAsync(store.Id, isActive, store.Version, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add(isActive ? $"{store.Name.Ja} を使います" : $"{store.Name.Ja} を使わなくしました", Severity.Success);
        await LoadAsync();
        Selection.NotifyChoicesChanged();
    }
}
