namespace TableOrder.HallApp.Modules.Visit;

using TableOrder.HallApp.Modules.Dialogs;

// 来店の詳細。状態、人数、開いた時刻と経過時間、注文の合計、注文と明細の状態を出し、人数の変更・席の移動・代わりの注文・食後の品のお願い・明細の取消・会計の手伝い・来店を終える操作を行う
// 来店と注文は開いたときと、席の一覧を読み直した知らせ (TablesChanged) で読み直し、来店が終わっていたら席のタブに戻る
// 来店を終える操作は、注文のある来店と会計中はレジで払った、注文のない来店は取りやめにする (サーバはどちらかしか通さない)
// 会計中は、注文を入れる・席を移る・食後の品のお願い・取消を押せない (会計の明細を替えないように画面で止める)
public sealed partial class VisitViewModel : AppViewModelBase
{
    // 経過時間を出し直す間隔
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<VisitViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly StoreState storeState;

    private readonly TableState tableState;

    private readonly IHallApi hallApi;

    private readonly HallUsecase hallUsecase;

    private Guid visitId;

    // 読んだ来店 (操作はこの版で送る)
    private VisitResponse? visit;

    // 取消を除いた明細がある
    private bool hasLines;

    [ObservableProperty]
    public partial string TitleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPaying { get; set; }

    [ObservableProperty]
    public partial string GuestText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    // 来店を読めた (読むまでは操作を押せない)
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    // 注文がない
    [ObservableProperty]
    public partial bool HasNoOrders { get; set; }

    // 取りやめにできる (取消を除いた明細がなく、会計中でない)。できなければ来店を終える操作をレジで払ったにする
    [ObservableProperty]
    public partial bool IsCancelable { get; set; }

    // 読めなかったときの知らせ
    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<VisitOrder> Orders { get; set; } = [];

    // 食後に出す品 (止めている明細) がある
    [ObservableProperty]
    public partial bool HasHeld { get; set; }

    [ObservableProperty]
    public partial string ReleaseText { get; set; } = string.Empty;

    public IObserveCommand BackCommand { get; }

    public IObserveCommand GuestsCommand { get; }

    public IObserveCommand MoveCommand { get; }

    public IObserveCommand BillCommand { get; }

    public IObserveCommand AddOrderCommand { get; }

    public IObserveCommand ReleaseCommand { get; }

    public IObserveCommand CancelLineCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CancelCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public VisitViewModel(
        ILogger<VisitViewModel> log,
        IPopupNavigator popupNavigator,
        TimeProvider timeProvider,
        StoreState storeState,
        TableState tableState,
        IHallApi hallApi,
        HallUsecase hallUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.storeState = storeState;
        this.tableState = tableState;
        this.hallApi = hallApi;
        this.hallUsecase = hallUsecase;

        BackCommand = MakeAsyncCommand(BackAsync);
        GuestsCommand = MakeAsyncCommand(ChangeGuestsAsync, () => IsLoaded);
        MoveCommand = MakeAsyncCommand(MoveAsync, () => IsLoaded && !IsPaying);
        BillCommand = MakeAsyncCommand(BillAsync, () => IsLoaded);
        AddOrderCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Order, Parameters.MakeVisit(visitId)), () => IsLoaded && !IsPaying);
        ReleaseCommand = MakeAsyncCommand(ReleaseAsync, () => IsLoaded && !IsPaying && HasHeld);
        CancelLineCommand = MakeAsyncCommand<VisitLine>(CancelLineAsync);
        CloseCommand = MakeAsyncCommand(CloseAsync, () => IsLoaded);
        CancelCommand = MakeAsyncCommand(CancelAsync, () => IsLoaded);

        Disposables.Add(Observable.Interval(TickInterval).ObserveOnCurrentContext().Subscribe(_ => UpdateOpened()));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 読むまでは、席の一覧の要約からテーブルの名前と状態を出しておく
    public override Task OnNavigatingToAsync(INavigationContext context)
    {
        visitId = context.Parameter.GetVisitId() ?? Guid.Empty;
        if (tableState.Items.FirstOrDefault(x => x.Visit?.VisitId == visitId) is { Visit: { } summary } table)
        {
            TitleText = ViewHelper.Table(table.Name);
            StatusText = ViewHelper.SeatStatus(summary.Status);
            IsPaying = summary.Status == VisitStatus.Paying;
            GuestText = ViewHelper.GuestDetail(summary.Adults, summary.Children);
        }

        return Task.CompletedTask;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        using (BusyState.Begin())
        {
            await LoadAsync();
        }
    }

    protected override Task OnNotifyBackAsync() => BackAsync();

    // 通知での読み直しはコマンドの外なので、読み直しの間は処理中にしてボタンと重ねない
    protected override async Task OnTablesChangedAsync()
    {
        using (BusyState.Begin())
        {
            await LoadAsync();
        }
    }

    // 開いたときの読み込み (遷移の途中) からも戻るので、遷移を終えてから戻る
    // 開いたときの読み込みと通知での読み直しが重なっても、戻るのは表示中のこの画面からの 1 回だけにする
    private async Task BackAsync() =>
        await Navigator.PostForwardAsync(this, ViewId.Seats);

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    // 来店と注文を読み直す。終わった来店と見つからない来店は席のタブに戻る
    private async Task LoadAsync()
    {
        var visitResult = await hallApi.GetVisitAsync(visitId);
        if (visitResult.ErrorCode == ErrorCodes.NotFound)
        {
            await BackAsync();
            return;
        }

        if (visitResult.Content is not { } current)
        {
            Fail(nameof(IHallApi.GetVisitAsync), visitResult);
            return;
        }

        if (current.Status is not (VisitStatus.Open or VisitStatus.Paying))
        {
            await BackAsync();
            return;
        }

        var ordersResult = await hallApi.GetOrdersAsync(visitId);
        if (ordersResult.Content is not { } orders)
        {
            Fail(nameof(IHallApi.GetOrdersAsync), ordersResult);
            return;
        }

        ErrorText = string.Empty;
        UpdateVisit(current);
        UpdateOrders(orders);
    }

    private void Fail<T>(string operation, ApiResult<T> result)
    {
        log.WarnApiFailed(operation, result.Status, result.ErrorCode);
        ErrorText = ViewHelper.ErrorMessage(result);
    }

    private void UpdateVisit(VisitResponse current)
    {
        visit = current;
        TitleText = ViewHelper.Table(current.TableName);
        StatusText = ViewHelper.SeatStatus(current.Status);
        IsPaying = current.Status == VisitStatus.Paying;
        GuestText = ViewHelper.GuestDetail(current.Adults, current.Children);
        TotalText = ViewHelper.Price(current.OrderTotal);
        IsLoaded = true;
        UpdateOpened();
        UpdateEnd();
    }

    private void UpdateOrders(OrderListResponse orders)
    {
        Orders = orders.Items.OrderBy(static x => x.OrderNo).Select(x => new VisitOrder(x, storeState.Store.TimeZone)).ToList();
        HasNoOrders = orders.Items.Count == 0;
        var lines = orders.Items.SelectMany(static x => x.Lines).ToList();
        hasLines = lines.Any(static x => x.Status != OrderLineStatus.Cancelled);
        var held = lines.Where(static x => x.Status == OrderLineStatus.Held).Sum(static x => x.Quantity);
        HasHeld = held > 0;
        ReleaseText = ViewHelper.Format(AppResources.VisitReleaseFormat, held);
        UpdateEnd();
    }

    private void UpdateEnd()
    {
        IsCancelable = !hasLines && !IsPaying;
    }

    private void UpdateOpened()
    {
        if (visit is not null)
        {
            OpenedText = ViewHelper.Format(
                AppResources.VisitOpenedFormat,
                ViewHelper.Time(visit.OpenedAt, storeState.Store.TimeZone),
                ViewHelper.Elapsed(timeProvider.GetUtcNow() - visit.OpenedAt));
        }
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private async Task ChangeGuestsAsync()
    {
        if (visit is null)
        {
            return;
        }

        var guests = await popupNavigator.GuestCountAsync(new GuestCountParameter(
            AppResources.GuestChangeTitle,
            string.Empty,
            visit.Adults,
            visit.Children,
            AppResources.GuestChange));
        if (guests is null)
        {
            return;
        }

        var result = await hallUsecase.UpdateGuestsAsync(visit, guests.Adults, guests.Children);
        await AfterOperationAsync(AppResources.GuestChangeTitle, result);
    }

    // 空いている席から選んで移る (確かめずに移す。戻すときは移り直す)
    private async Task MoveAsync()
    {
        if (visit is null)
        {
            return;
        }

        var tables = tableState.Items
            .Where(static x => x.Visit is null)
            .Select(static x => new TableChoice(x.Id, ViewHelper.Table(x.Name), ViewHelper.Format(AppResources.CapacityFormat, x.Capacity)))
            .ToList();
        if (await popupNavigator.SelectTableAsync(new MoveTableParameter(tables)) is not { } tableId)
        {
            return;
        }

        var result = await hallUsecase.MoveVisitAsync(visit, tableId);
        await AfterOperationAsync(AppResources.VisitMove, result);
    }

    // 会計の明細を見せ、会計を始めるか取りやめる (どちらにするかは明細のポップアップが状態で決める)
    private async Task BillAsync()
    {
        if (visit is null)
        {
            return;
        }

        var billResult = await hallApi.GetBillAsync(visitId);
        if (billResult.Content is not { } bill)
        {
            log.WarnApiFailed(nameof(IHallApi.GetBillAsync), billResult.Status, billResult.ErrorCode);
            await popupNavigator.MessageAsync(AppResources.VisitBill, ViewHelper.ErrorMessage(billResult));
            return;
        }

        var title = ViewHelper.Format(AppResources.BillTitleFormat, visit.TableName);
        var action = await popupNavigator.BillAsync(new BillParameter(title, bill, IsPaying));
        if (action == BillAction.StartCheckout)
        {
            await AfterOperationAsync(AppResources.BillStart, await hallUsecase.StartCheckoutAsync(visit, bill.BillVersion));
        }
        else if (action == BillAction.CancelCheckout)
        {
            var cancelled = await hallUsecase.CancelCheckoutAsync(visit);
            await AfterOperationAsync(AppResources.BillCancel, cancelled);

            // 明細を出したあとに払い終えた支払があると、サーバは取りやめずに会計中のまま返す
            if (cancelled.Content is { Status: VisitStatus.Paying })
            {
                await popupNavigator.MessageAsync(AppResources.BillCancel, AppResources.BillCancelUnavailable);
            }
        }
    }

    // 食後の品をすべてお願いする
    private async Task ReleaseAsync()
    {
        if ((visit is null) ||
            !await popupNavigator.ConfirmAsync(AppResources.ReleaseTitle, ViewHelper.Format(AppResources.ReleaseMessageFormat, visit.TableName), AppResources.ReleaseOk, AppResources.CommonBack))
        {
            return;
        }

        await AfterOrderChangeAsync(AppResources.ReleaseTitle, await hallUsecase.ReleaseAsync(visit));
    }

    // 提供と取消を除く明細を取り消す (数量が 2 以上なら取り消す数を選ぶ)。会計中はサーバが断るので開かない
    private async Task CancelLineAsync(VisitLine line)
    {
        if (!IsLoaded || IsPaying || !line.CanCancel)
        {
            return;
        }

        if (await popupNavigator.LineCancelAsync(new LineCancelParameter(line.Name, line.OptionText, line.Quantity)) is not { } quantity)
        {
            return;
        }

        // 取消で断られる明細は、ほかの端末が先に提供したか取り消したもの (提供のときとは理由が違う)
        var result = await hallUsecase.CancelLineAsync(line.OrderId, line.LineId, quantity);
        await AfterOrderChangeAsync(AppResources.LineCancelTitle, result, result.ErrorCode == ErrorCodes.LineStatusInvalid ? AppResources.ErrorLineCancelInvalid : null);
    }

    // 注文を替えたら来店と注文を読み直す (合計も替わる)。断られたら知らせてから読み直す
    private async Task AfterOrderChangeAsync<T>(string title, ApiResult<T> result, string? message = null)
    {
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, message ?? ViewHelper.ErrorMessage(result));
        }

        await LoadAsync();
    }

    private async Task CloseAsync()
    {
        if ((visit is null) ||
            !await popupNavigator.ConfirmAsync(AppResources.CloseTitle, ViewHelper.Format(AppResources.CloseMessageFormat, visit.TableName), AppResources.CloseOk, AppResources.CommonBack))
        {
            return;
        }

        var result = await hallUsecase.CloseVisitAsync(visit);
        await AfterEndAsync(AppResources.CloseTitle, result);
    }

    private async Task CancelAsync()
    {
        if ((visit is null) ||
            !await popupNavigator.ConfirmAsync(AppResources.CancelTitle, ViewHelper.Format(AppResources.CancelMessageFormat, visit.TableName), AppResources.CancelOk, AppResources.CommonBack))
        {
            return;
        }

        var result = await hallUsecase.CancelVisitAsync(visit);
        await AfterEndAsync(AppResources.CancelTitle, result);
    }

    // 変えた来店を出す。断られたら知らせて読み直す (ほかで替えられていた、会計中になっていたなど)
    private async Task AfterOperationAsync(string title, ApiResult<VisitResponse> result)
    {
        if (result.Content is { } changed)
        {
            UpdateVisit(changed);
            return;
        }

        await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        await LoadAsync();
    }

    // 終えたら席のタブに戻る。断られたら知らせて読み直す
    private async Task AfterEndAsync(string title, ApiResult<VisitResponse> result)
    {
        if (result.IsSuccess)
        {
            await BackAsync();
            return;
        }

        await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        await LoadAsync();
    }
}
