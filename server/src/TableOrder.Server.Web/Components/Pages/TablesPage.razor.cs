namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Server.Web.Application.Context;

// テーブルの管理 (選んだ店舗のテーブルの一覧、追加、名前・エリア・席の数の変更、並べ替え、使わなくする)
public sealed partial class TablesPage : IDisposable
{
    private List<TableSetupEntity> tables = [];

    private string addName = string.Empty;

    private string addArea = string.Empty;

    private int addCapacity = 4;

    private TableSetupEntity? editing;

    private string editName = string.Empty;

    private string editArea = string.Empty;

    private int editCapacity;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required TableSetupService TableSetupService { get; set; }

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

    // 店舗を選び直したら読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
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
        tables = Selection.StoreId is null ? [] : await TableSetupService.GetListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Table
    //--------------------------------------------------------------------------------

    private async Task AddAsync()
    {
        if (!Check(await TableSetupService.AddAsync(new TableInput(addName, addArea, addCapacity), CancellationToken.None)))
        {
            return;
        }

        Snackbar.Add($"テーブル {addName.Trim()} を足しました", Severity.Success);
        addName = string.Empty;
        addArea = string.Empty;
        await LoadAsync();
    }

    private void Edit(TableSetupEntity table)
    {
        editing = table;
        editName = table.Name;
        editArea = table.Area ?? string.Empty;
        editCapacity = table.Capacity;
    }

    private void CancelEdit() => editing = null;

    private async Task SaveAsync()
    {
        if (editing is not { } table ||
            !Check(await TableSetupService.UpdateAsync(table.Id, new TableInput(editName, editArea, editCapacity), table.Version, CancellationToken.None)))
        {
            return;
        }

        Snackbar.Add($"テーブル {editName.Trim()} を替えました", Severity.Success);
        await LoadAsync();
    }

    private async Task MoveAsync(TableSetupEntity table, int offset)
    {
        if (Check(await TableSetupService.MoveAsync(table.Id, offset, CancellationToken.None)))
        {
            await LoadAsync();
        }
    }

    private async Task SetActiveAsync(bool isActive)
    {
        if (editing is not { } table ||
            !Check(await TableSetupService.SetActiveAsync(table.Id, isActive, table.Version, CancellationToken.None)))
        {
            return;
        }

        Snackbar.Add(isActive ? $"テーブル {table.Name} を使います" : $"テーブル {table.Name} を使わなくしました", Severity.Success);
        await LoadAsync();
    }

    // 失敗を知らせる (成功なら true)
    private bool Check(ServiceError? error)
    {
        if (error is null)
        {
            return true;
        }

        Snackbar.Add(AdminNames.ErrorMessage(error), Severity.Error);
        return false;
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private bool IsFirst(TableSetupEntity table) => (tables.Count > 0) && (tables[0].Id == table.Id);

    private bool IsLast(TableSetupEntity table) => (tables.Count > 0) && (tables[^1].Id == table.Id);

    private static string StatusText(TableSetupEntity table) =>
        !table.IsActive ? "使っていない" : table.HasOpenVisit ? "来店中" : "使う";
}
