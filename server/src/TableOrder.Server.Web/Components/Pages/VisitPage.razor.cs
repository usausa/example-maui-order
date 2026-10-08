namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;
using TableOrder.Server.Web.Application.Context;

// 案内 (ホール端末ができるまでの仮の画面)。選んだ店舗のテーブルの一覧と、来店を開く・閉じる (レジで払った)・取りやめ
// ホール端末の API と同じ VisitService を呼ぶので、テーブル端末には同じ通知が届く
public sealed partial class VisitPage : IDisposable
{
    private List<TableListResponseItem> tables = [];

    private string timeZone = string.Empty;

    private Guid? guidingTableId;

    private int guideAdults;

    private int guideChildren;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required VisitService VisitService { get; set; }

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

    // 読み直すと案内の欄を閉じる (テーブル端末での会計などで、表示のあとに来店が変わっていることがある)
    private async Task LoadAsync()
    {
        guidingTableId = null;
        if (Selection.StoreId is null)
        {
            tables = [];
            return;
        }

        timeZone = (await StoreService.GetStoreAsync(CancellationToken.None))?.TimeZone ?? string.Empty;
        tables = (await StoreService.GetTablesAsync(null, CancellationToken.None)).Value?.Items.ToList() ?? [];
    }

    //--------------------------------------------------------------------------------
    // Guide
    //--------------------------------------------------------------------------------

    private void Guide(TableListResponseItem table)
    {
        guidingTableId = table.Id;
        guideAdults = 2;
        guideChildren = 0;
    }

    private void CancelGuide() => guidingTableId = null;

    private async Task OpenAsync(TableListResponseItem table)
    {
        var result = await VisitService.CreateAsync(
            new VisitCreateRequest
            {
                Id = Guid.CreateVersion7(),
                TableId = table.Id,
                Adults = guideAdults,
                Children = guideChildren
            },
            CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"テーブル {table.Name} の来店を開きました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    // レジで払った来店を終える (会計中も閉じられる)
    private async Task CloseAsync(TableListResponseItem table)
    {
        if ((table.Visit is not { } visit) || !await ConfirmAsync("来店を閉じる", $"テーブル {table.Name} の来店を、レジで払ったとして閉じます。テーブル端末は待受に戻ります。", "閉じる"))
        {
            return;
        }

        var result = await VisitService.CloseAsync(visit.VisitId, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version }, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"テーブル {table.Name} の来店を閉じました", Severity.Success);
        await LoadAsync();
    }

    // 注文のないまま帰った来店を取りやめる
    private async Task CancelAsync(TableListResponseItem table)
    {
        if ((table.Visit is not { } visit) || !await ConfirmAsync("来店を取りやめる", $"テーブル {table.Name} の来店を、注文のないまま帰ったとして取りやめます。テーブル端末は待受に戻ります。", "取りやめる"))
        {
            return;
        }

        var result = await VisitService.CancelAsync(visit.VisitId, new VisitCancelRequest { Version = visit.Version }, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"テーブル {table.Name} の来店を取りやめました", Severity.Success);
        await LoadAsync();
    }

    private async Task<bool> ConfirmAsync(string title, string message, string yesText) =>
        await DialogService.ShowMessageBoxAsync(title, message, yesText: yesText, cancelText: "やめる") == true;

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private static string StatusText(TableListResponseVisit? visit) =>
        visit?.Status switch
        {
            null => "空き",
            VisitStatus.Paying => "会計中",
            _ => "来店中"
        };

    private static string GuestText(TableListResponseVisit visit) =>
        visit.Children > 0 ? $"大人 {visit.Adults}・子ども {visit.Children}" : $"大人 {visit.Adults}";

    private static string CountText(int? count) =>
        count?.ToString(CultureInfo.InvariantCulture) ?? "--";

    // 店舗の現地の時刻で出す
    private string FormatTime(DateTimeOffset value) =>
        StoreHours.LocalDateTime(value, timeZone).ToString("M/d HH:mm", CultureInfo.InvariantCulture);

    private static string ErrorMessage(ServiceError error) =>
        error.Errors?.Values.SelectMany(static x => x).FirstOrDefault() ?? error.ErrorCode switch
        {
            ErrorCodes.TableOccupied => "このテーブルには来店があります。読み直してください",
            ErrorCodes.VisitHasOrders => "注文のある来店は取りやめられません。レジで払ったら閉じてください",
            ErrorCodes.CheckoutInProgress => "会計中の来店は取りやめられません",
            ErrorCodes.VisitNotOpen => "来店は終わっています。読み直してください",
            ErrorCodes.VersionMismatch => "ほかで替えられています。読み直してください",
            ErrorCodes.NotFound => "見つかりません。読み直してください",
            _ => error.ErrorCode
        };
}
