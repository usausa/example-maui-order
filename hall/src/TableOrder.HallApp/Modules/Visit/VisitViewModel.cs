namespace TableOrder.HallApp.Modules.Visit;

using TableOrder.HallApp.Modules.Dialogs;

// 来店の詳細。状態、人数、開いた時刻と経過時間、注文の合計、注文と明細の状態を出し、人数の変更・席の移動・来店を終える操作を行う
// 来店と注文は開いたときと、席の一覧を読み直した知らせ (TablesChanged) で読み直し、来店が終わっていたら席のタブに戻る
// 来店を終える操作は、注文のある来店と会計中はレジで払った、注文のない来店は取りやめにする (サーバはどちらかしか通さない)
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

    public IObserveCommand BackCommand { get; }

    public IObserveCommand GuestsCommand { get; }

    public IObserveCommand MoveCommand { get; }

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

    protected override Task OnTablesChangedAsync() => LoadAsync();

    private async Task BackAsync() =>
        await Navigator.ForwardAsync(ViewId.Seats);

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
        hasLines = orders.Items.SelectMany(static x => x.Lines).Any(static x => x.Status != OrderLineStatus.Cancelled);
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
