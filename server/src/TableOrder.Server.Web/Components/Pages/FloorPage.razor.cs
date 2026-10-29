namespace TableOrder.Server.Web.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using TableOrder.Contract.Events;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;
using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Hubs;

// 店内状況。選んだ店舗のテーブルと来店、選んだ来店の注文、呼び出し、品切れ、通知履歴を出し、店舗の通知を送るたびに読み直す
// テーブルを押すと右の欄に来店を出し、ホール端末が使えないときは、そこで来店を開く・閉じる・取りやめる
// (ホール端末と同じ VisitService を呼ぶので、テーブル端末には同じ通知が届く)
public sealed partial class FloorPage : IDisposable
{
    // 通知履歴に出す数
    private const int RecentEventCount = 20;

    // 通知はまとまって届くので、少し待ってから 1 回だけ読み直す
    private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(300);

    // 経過時間を出し直す間隔
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource disposing = new();

    private List<TableListResponseItem> tables = [];

    private List<OrderListResponseItem> orders = [];

    private List<FloorCallResult> calls = [];

    private List<FloorStockResult> stock = [];

    private List<EventEntity> events = [];

    private string timeZone = string.Empty;

    private DateTimeOffset? loadedAt;

    private Guid? selectedTableId;

    private int guideAdults;

    private int guideChildren;

    private IDisposable? watcher;

    private Timer? clock;

    // 読み直しを待っているか (通知の送り手のスレッドと画面の文脈から触る)
    private int reloadScheduled;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required StoreSelection Selection { get; set; }

    [Inject]
    public required StoreActivity Activity { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required VisitService VisitService { get; set; }

    [Inject]
    public required OrderService OrderService { get; set; }

    [Inject]
    public required FloorService FloorService { get; set; }

    [Inject]
    public required EventService EventService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private TableListResponseItem? SelectedTable => tables.Find(x => x.Id == selectedTableId);

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override Task OnInitializedAsync()
    {
        Selection.Changed += OnSelectionChanged;
        clock = new Timer(_ => InvokeAsync(StateHasChanged), null, ClockInterval, ClockInterval);
        WatchStore();
        return LoadAsync();
    }

    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
        watcher?.Dispose();
        clock?.Dispose();
        disposing.Cancel();
        disposing.Dispose();
    }

    // 店舗を選び直したら、見る店舗を替えて読み直す (店舗の選択の操作の中から呼ばれるので、文脈を始め直す)
    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _ = ReloadAsync(() =>
        {
            selectedTableId = null;
            WatchStore();
            return LoadAsync();
        });

    // 選んだ店舗の通知を見る
    private void WatchStore()
    {
        watcher?.Dispose();
        watcher = (Selection.TenantId is { } tenantId) && (Selection.StoreId is { } storeId) ? Activity.Watch(tenantId, storeId, OnStoreChanged) : null;
    }

    // 店舗の通知を送った (通知の送り手のスレッドから呼ばれる)
    private void OnStoreChanged()
    {
        if (Interlocked.Exchange(ref reloadScheduled, 1) == 0)
        {
            _ = ReloadLaterAsync();
        }
    }

    // 読み直す前に印を戻し、読み直す間に届いた通知でもう一度読み直す (変わったことを取りこぼさない)
    private async Task ReloadLaterAsync()
    {
        try
        {
            await Task.Delay(ReloadDelay, disposing.Token);
            Volatile.Write(ref reloadScheduled, 0);
            await ReloadAsync(LoadAsync);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // 画面を閉じた
        }
    }

    // 読み直しても選んだテーブルと入れている人数は替えない (人数を入れている間に、ほかの通知で読み直すことがある)
    private async Task LoadAsync()
    {
        if (Selection.StoreId is null)
        {
            tables = [];
            orders = [];
            calls = [];
            stock = [];
            events = [];
            loadedAt = null;
            return;
        }

        timeZone = (await StoreService.GetStoreAsync(CancellationToken.None))?.TimeZone ?? string.Empty;
        tables = (await StoreService.GetTablesAsync(null, CancellationToken.None)).Value?.Items.ToList() ?? [];
        await LoadOrdersAsync();
        calls = await FloorService.GetCallsAsync(CancellationToken.None);
        stock = await FloorService.GetStockAsync(CancellationToken.None);
        events = await EventService.GetRecentAsync(RecentEventCount, CancellationToken.None);
        loadedAt = TimeProvider.GetUtcNow();
    }

    // 選んだテーブルの今の来店の注文 (来店がなければ出さない)
    private async Task LoadOrdersAsync()
    {
        orders = SelectedTable?.Visit is { } visit
            ? (await OrderService.GetListAsync(visit.VisitId, CancellationToken.None)).Value?.Items.ToList() ?? []
            : [];
    }

    //--------------------------------------------------------------------------------
    // Select
    //--------------------------------------------------------------------------------

    // 選び直したら、案内の人数をはじめの値にする
    private Task SelectAsync(TableRowClickEventArgs<TableListResponseItem> args)
    {
        if ((args.Item is not { } table) || (table.Id == selectedTableId))
        {
            return Task.CompletedTask;
        }

        selectedTableId = table.Id;
        guideAdults = 2;
        guideChildren = 0;
        return LoadOrdersAsync();
    }

    // 取りやめられるのは、取消を除いた明細のない来店だけ (注文のある来店はレジで払ってから閉じる)
    private bool CanCancel(TableListResponseVisit visit) =>
        (visit.Status == VisitStatus.Open) && orders.SelectMany(static x => x.Lines).All(static x => x.Status == OrderLineStatus.Cancelled);

    private string RowClass(TableListResponseItem table) =>
        table.Id == selectedTableId ? "floor-selected" : string.Empty;

    //--------------------------------------------------------------------------------
    // Guide
    //--------------------------------------------------------------------------------

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

        Snackbar.Add($"テーブル {table.Name} の来店を登録しました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    // レジで払った来店を終える (会計中も閉じられる)
    // レジで払ったお客様は帰っていることもあるので、お願いしていない食後の品は自動ではお願いせず、閉じる前に知らせる
    private async Task CloseAsync(TableListResponseItem table)
    {
        if (table.Visit is not { } visit)
        {
            return;
        }

        var held = orders.SelectMany(static x => x.Lines).Any(static x => x.Status == OrderLineStatus.Held) ? " 依頼前の食後の品は作りません" : string.Empty;
        if (!await DialogService.ConfirmAsync("レジ会計済み", $"テーブル {table.Name} の来店を終了しますか？{held}", "終了"))
        {
            return;
        }

        var result = await VisitService.CloseAsync(visit.VisitId, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version }, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"テーブル {table.Name} の来店を終了しました", Severity.Success);
        await LoadAsync();
    }

    // 注文のないまま帰った来店を取りやめる
    private async Task CancelAsync(TableListResponseItem table)
    {
        if ((table.Visit is not { } visit) || !await DialogService.ConfirmAsync("来店取消", $"テーブル {table.Name} の来店を取り消しますか？", "取消"))
        {
            return;
        }

        var result = await VisitService.CancelAsync(visit.VisitId, new VisitCancelRequest { Version = visit.Version }, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(ErrorMessage(result.Error), Severity.Error);
            return;
        }

        Snackbar.Add($"テーブル {table.Name} の来店を取り消しました", Severity.Success);
        await LoadAsync();
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    private static string StatusText(TableListResponseVisit? visit) =>
        visit is null ? "空き" : AdminNames.VisitStatusName(visit.Status);

    private static string GuestText(TableListResponseVisit visit) =>
        visit.Children > 0
            ? $"大人 {AdminNames.FormatCount(visit.Adults)}・子ども {AdminNames.FormatCount(visit.Children)}"
            : $"大人 {AdminNames.FormatCount(visit.Adults)}";

    private static string CountText(int? count) =>
        count is { } value ? AdminNames.FormatCount(value) : "--";

    private string ElapsedText(DateTimeOffset openedAt)
    {
        var minutes = Math.Max(0, (int)(TimeProvider.GetUtcNow() - openedAt).TotalMinutes);
        return minutes < 60 ? $"{minutes} 分" : $"{minutes / 60} 時間 {minutes % 60} 分";
    }

    // 品の名前に、選んだオプションを添える
    private static string LineText(OrderListResponseLine line) =>
        line.Options.Count > 0 ? $"{line.Name.Ja} ({String.Join("、", line.Options.Select(static x => x.Name.Ja))})" : line.Name.Ja;

    // メニューにない品 (公開し直して外した品) は Id で出す
    private static string StockName(FloorStockResult item)
    {
        var name = item.Name?.Ja ?? item.Stock.TargetId.ToString("D");
        return item.Stock.TargetKind == StockTargetKind.Option ? $"{name} (オプション)" : name;
    }

    private static string StockText(StockListResponseItem item) =>
        item.Status == StockStatus.Limited ? $"残り {AdminNames.FormatCount(item.Remaining.GetValueOrDefault())}" : "品切れ";

    private static string EventName(string type) =>
        type switch
        {
            EventTypes.VisitOpened => "来店登録",
            EventTypes.VisitUpdated => "来店の更新",
            EventTypes.VisitMoved => "席の移動",
            EventTypes.VisitClosed => "来店終了",
            EventTypes.OrderCreated => "注文受付",
            EventTypes.OrderLinesUpdated => "明細の更新",
            EventTypes.TicketCreated => "チケット作成",
            EventTypes.TicketUpdated => "チケットの更新",
            EventTypes.CallCreated => "呼び出し",
            EventTypes.CallUpdated => "呼び出しの更新",
            EventTypes.StockUpdated => "品切れの更新",
            EventTypes.MenuPublished => "メニュー公開",
            EventTypes.StoreUpdated => "店舗の更新",
            EventTypes.DeviceUpdated => "端末の更新",
            EventTypes.PaymentUpdated => "支払の更新",
            _ => type
        };

    // 店舗の現地の時刻で出す (今日の動きなので時刻だけ)
    private string FormatTime(DateTimeOffset value) => AdminNames.FormatTime(value, timeZone);

    private static string ErrorMessage(ServiceError error) =>
        AdminNames.ErrorMessage(error, static code => code switch
        {
            ErrorCodes.TableOccupied => "このテーブルはすでに来店中です",
            ErrorCodes.VisitHasOrders => "注文のある来店は取り消せません",
            ErrorCodes.CheckoutInProgress => "会計中の来店は取り消せません",
            ErrorCodes.VisitNotOpen => "来店はすでに終了しています",
            _ => null
        });
}
