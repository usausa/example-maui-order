namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Contract.Menu;
using TableOrder.Contract.Stores;
using TableOrder.Server.Web.Application.Context;

// 端末の管理 (選んだ店舗の端末の一覧、ペアリングコードと登録トークンの発行、登録トークンの取り消し、置き場所の割り当てと名前の変更、無効化)
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

    private List<DeviceEnrollmentEntity> tokens = [];

    private DeviceKind tokenKind = DeviceKind.Table;

    private int tokenMaxUses = 10;

    private int tokenDays = 7;

    private EnrollmentTokenResult? issuedToken;

    private DeviceSummaryResult? editing;

    private string editName = string.Empty;

    private Guid? editTableId;

    private IReadOnlyCollection<Guid> editStationIds = [];

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

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

    // 店舗を選び直したら、出したコードとトークンと選んだ置き場所を消して読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = InvokeAsync(async () =>
        {
            issued = null;
            issuedToken = null;
            SelectIssueKind(issueKind);
            using (BeginServiceScope())
            {
                await LoadAsync();
            }

            StateHasChanged();
        });

    // 読み直すと変更の欄を閉じる (出したペアリングコードと登録トークンは、端末と EMM に入れ終えるまで出しておく)
    private async Task LoadAsync()
    {
        editing = null;
        if (Selection.StoreId is null)
        {
            devices = [];
            tables = [];
            stations = [];
            tokens = [];
            return;
        }

        timeZone = (await StoreService.GetStoreAsync(CancellationToken.None))?.TimeZone ?? string.Empty;
        devices = await DeviceService.GetSummaryListAsync(CancellationToken.None);
        tables = (await StoreService.GetTablesAsync(null, CancellationToken.None)).Value?.Items.ToList() ?? [];
        stations = await DeviceService.GetStationListAsync(CancellationToken.None);
        tokens = await EnrollmentService.GetTokenListAsync(CancellationToken.None);
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
    // Enrollment token
    //--------------------------------------------------------------------------------

    private async Task IssueTokenAsync()
    {
        var result = await EnrollmentService.IssueEnrollmentTokenAsync(tokenKind, tokenMaxUses, tokenDays, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        issuedToken = result.Value;
        tokens = await EnrollmentService.GetTokenListAsync(CancellationToken.None);
    }

    // 取り消すと、それからの登録を断る (登録した端末はそのまま)。出したばかりのトークンなら、見せている値も消す
    private async Task RevokeTokenAsync(DeviceEnrollmentEntity token)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "登録トークンを取り消す",
            $"{FormatTime(token.CreatedAt)} に出した{KindName(token.Kind)}の登録トークンを取り消します。それからの登録を断ります (登録した端末はそのまま使えます)。",
            yesText: "取り消す",
            cancelText: "やめる");
        if (confirmed != true)
        {
            return;
        }

        var error = await EnrollmentService.RevokeTokenAsync(token.Id, CancellationToken.None);
        if (error is not null)
        {
            Snackbar.Add(ErrorMessage(error), Severity.Error);
            return;
        }

        if (issuedToken?.Id == token.Id)
        {
            issuedToken = null;
        }

        Snackbar.Add("登録トークンを取り消しました", Severity.Success);
        tokens = await EnrollmentService.GetTokenListAsync(CancellationToken.None);
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

    // 管理対象の構成の接続先 (この管理画面のサーバの URL)
    private string ApiEndPoint => Navigation.BaseUri;

    private bool IsUsable(DeviceEnrollmentEntity token) =>
        (token.RevokedAt is null) && (token.ExpiresAt > TimeProvider.GetUtcNow()) && (token.UsedCount < token.MaxUses);

    private string TokenStatusText(DeviceEnrollmentEntity token) =>
        token.RevokedAt is not null ? "取り消した" :
        token.ExpiresAt <= TimeProvider.GetUtcNow() ? "期限切れ" :
        token.UsedCount >= token.MaxUses ? "使い切った" :
        "使える";

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
