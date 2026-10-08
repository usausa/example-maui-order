namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Contract.Menu;
using TableOrder.Contract.Stores;
using TableOrder.Server.Web.Application.Context;

// 端末の管理 (選んだ店舗の端末の一覧、ペアリングコードの発行、置き場所と名前の変更、無効化)
public sealed partial class DevicePage : IDisposable
{
    private static readonly DeviceKind[] Kinds = [DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen, DeviceKind.Reception];

    private List<DeviceSummaryResult> devices = [];

    private List<TableListResponseItem> tables = [];

    private List<MenuResponseStation> stations = [];

    private string timeZone = string.Empty;

    private DeviceKind issueKind = DeviceKind.Table;

    private Guid? issueTableId;

    private IReadOnlyCollection<Guid> issueStationIds = [];

    private PairingCodeResult? issued;

    private DeviceSummaryResult? editing;

    private string editName = string.Empty;

    private Guid? editTableId;

    private IReadOnlyCollection<Guid> editStationIds = [];

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required DeviceService DeviceService { get; set; }

    [Inject]
    public required DeviceEnrollmentService EnrollmentService { get; set; }

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

    // 店舗を選び直したら、出したコードと選んだ置き場所を消して読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(async () =>
        {
            issued = null;
            SelectIssueKind(issueKind);
            using (BeginServiceScope())
            {
                await LoadAsync();
            }

            StateHasChanged();
        });

    // 読み直すと変更の欄を閉じる (出したペアリングコードは、端末に入れ終えるまで出しておく)
    private async Task LoadAsync()
    {
        editing = null;
        if (Selection.StoreId is null)
        {
            devices = [];
            tables = [];
            stations = [];
            return;
        }

        timeZone = (await StoreService.GetStoreAsync(CancellationToken.None))?.TimeZone ?? string.Empty;
        devices = await DeviceService.GetSummaryListAsync(CancellationToken.None);
        tables = (await StoreService.GetTablesAsync(null, CancellationToken.None)).Value?.Items.ToList() ?? [];
        stations = await DeviceService.GetStationListAsync(CancellationToken.None);
    }

    //--------------------------------------------------------------------------------
    // Pairing code
    //--------------------------------------------------------------------------------

    private void SelectIssueKind(DeviceKind kind)
    {
        issueKind = kind;
        issueTableId = null;
        issueStationIds = [];
    }

    private async Task IssueAsync()
    {
        var result = await EnrollmentService.IssuePairingCodeAsync(
            issueKind,
            issueKind == DeviceKind.Table ? issueTableId : null,
            issueKind == DeviceKind.Kitchen ? issueStationIds.ToList() : [],
            CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        issued = result.Value;
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private void Edit(DeviceSummaryResult item)
    {
        editing = item;
        editName = item.Device.Name;
        editTableId = item.Device.TableId;
        editStationIds = item.StationIds;
    }

    private void CancelEdit() => editing = null;

    private async Task SaveAsync()
    {
        if (editing is not { } item)
        {
            return;
        }

        var error = await DeviceService.UpdateAsync(
            item.Device.Id,
            editName,
            item.Device.Kind == DeviceKind.Table ? editTableId : null,
            item.Device.Kind == DeviceKind.Kitchen ? editStationIds.ToList() : [],
            item.Device.Version,
            CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{editName.Trim()} を替えました。端末は起動からやり直します", Severity.Success);
        await LoadAsync();
    }

    private async Task RevokeAsync()
    {
        if (editing is not { } item)
        {
            return;
        }

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "端末を無効にする",
            $"{item.Device.Name} を無効にします。端末は登録を消して、端末の設定の画面に戻ります。",
            yesText: "無効にする",
            cancelText: "やめる");
        if (confirmed != true)
        {
            return;
        }

        var error = await DeviceService.RevokeAsync(item.Device.Id, item.Device.Version, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(ErrorMessage(error), Severity.Error);
            return;
        }

        Snackbar.Add($"{item.Device.Name} を無効にしました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private static string KindName(DeviceKind kind) =>
        kind switch
        {
            DeviceKind.Table => "テーブル端末",
            DeviceKind.Hall => "ホール端末",
            DeviceKind.Kitchen => "キッチン端末",
            DeviceKind.Reception => "受付機",
            _ => kind.ToString()
        };

    private string PlacementText(DeviceSummaryResult item) =>
        item.Device.Kind switch
        {
            DeviceKind.Table => item.Device.TableName is { } table ? $"テーブル {table}" : "未割り当て",
            DeviceKind.Kitchen => item.StationIds.Count > 0 ? String.Join("、", item.StationIds.Select(StationName)) : "未割り当て",
            _ => "--"
        };

    private string StationName(Guid id) =>
        stations.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString("D");

    private static string BatteryText(DeviceSummaryEntity device) =>
        device.BatteryLevel is { } level
            ? $"{Math.Round(level * 100, MidpointRounding.AwayFromZero)}%{(device.IsCharging == true ? " (充電中)" : string.Empty)}"
            : "--";

    // 店舗の現地の時刻で出す
    private string FormatTime(DateTimeOffset value) =>
        StoreHours.LocalDateTime(value, timeZone).ToString("M/d HH:mm", CultureInfo.InvariantCulture);

    private static string ErrorMessage(ServiceError error) =>
        error.Errors?.Values.SelectMany(static x => x).FirstOrDefault() ?? error.ErrorCode switch
        {
            ErrorCodes.NotFound => "見つかりません。読み直してください",
            ErrorCodes.VersionMismatch => "ほかで替えられたか、無効にされています。読み直してください",
            _ => error.ErrorCode
        };
}
