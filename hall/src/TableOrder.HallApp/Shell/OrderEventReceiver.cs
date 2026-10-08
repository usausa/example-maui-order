namespace TableOrder.HallApp.Shell;

using TableOrder.Terminal.Components;
using TableOrder.Terminal.Shell;

// ホール端末の通知の扱い。店舗と品切れは通知の中身で替え、席・呼び出し・提供は変わった一覧を読み直して、表示中の画面に知らせる
// まとめて届いた通知は扱い終えたあと (OnDrainedAsync) に一覧を 1 回だけ読み直し、読み直せなかった一覧は次の通知のあとに読み直す
// 受け方 (重複を捨てる、操作と遷移の間を待つ、起動からやり直す知らせ) は土台 (OrderEventReceiverBase) が行う
public sealed class OrderEventReceiver : OrderEventReceiverBase
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly StoreState storeState;

    private readonly MenuState menuState;

    private readonly HallUsecase hallUsecase;

    // 読み直す一覧
    private bool tablesChanged;

    private bool callsChanged;

    private bool servingChanged;

    public OrderEventReceiver(
        ILogger<OrderEventReceiver> log,
        INavigator navigator,
        IReactiveMessenger messenger,
        ManagedConfiguration managedConfiguration,
        IBusyState busyState,
        Settings settings,
        StoreState storeState,
        MenuState menuState,
        IDeviceApi deviceApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase,
        HallUsecase hallUsecase)
        : base(log, navigator, messenger, managedConfiguration, busyState, settings, deviceApi, events, deviceUsecase)
    {
        this.log = log;
        this.storeState = storeState;
        this.menuState = menuState;
        this.hallUsecase = hallUsecase;
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // 読み直す一覧の印は届いたときに付ける (読み直すのは、待っていた通知を扱い終えたとき)
    protected override void OnReceived(OrderEvent e)
    {
        switch (e)
        {
            case VisitOpenedEvent or VisitUpdatedEvent or OrderCreatedEvent:
                tablesChanged = true;
                break;
            case VisitMovedEvent or VisitClosedEvent:
                // 呼び出しと提供の一覧は来店の今のテーブルを出し、閉じた来店のものは外れるので、一緒に読み直す
                tablesChanged = true;
                callsChanged = true;
                servingChanged = true;
                break;
            case OrderLinesUpdatedEvent:
                tablesChanged = true;
                servingChanged = true;
                break;
            case CallCreatedEvent or CallUpdatedEvent:
                tablesChanged = true;
                callsChanged = true;
                break;
        }
    }

    // 起動ですべて読み直すので、読み直す一覧の印は消す
    protected override async Task NotifyRestartAsync()
    {
        tablesChanged = false;
        callsChanged = false;
        servingChanged = false;
        await Navigator.NotifyAsync(ShellEvent.Restart).ConfigureAwait(true);
    }

    // 店舗と品切れは通知の中身で替える (ほかの通知は印を付けたので、ここでは何もしない)
    protected override async Task ApplyAsync(OrderEvent e)
    {
        switch (e)
        {
            case StoreUpdatedEvent updated:
                storeState.UpdateStore(updated.Store);
                await Navigator.NotifyAsync(ShellEvent.StoreChanged).ConfigureAwait(true);
                break;
            case StockUpdatedEvent stock:
                menuState.ApplyStock(stock.Items);
                await Navigator.NotifyAsync(ShellEvent.StockChanged).ConfigureAwait(true);
                break;
        }
    }

    protected override async Task OnDrainedAsync()
    {
        if (tablesChanged)
        {
            tablesChanged = !await RefreshAsync(hallUsecase.RefreshTablesAsync, nameof(IHallApi.GetTablesAsync), ShellEvent.TablesChanged).ConfigureAwait(true);
        }

        if (callsChanged)
        {
            callsChanged = !await RefreshAsync(hallUsecase.RefreshCallsAsync, nameof(IHallApi.GetCallsAsync), ShellEvent.CallsChanged).ConfigureAwait(true);
        }

        if (servingChanged)
        {
            servingChanged = !await RefreshAsync(hallUsecase.RefreshServingAsync, nameof(IHallApi.GetServingAsync), ShellEvent.ServingChanged).ConfigureAwait(true);
        }
    }

    // 読み直せたら表示中の画面に知らせる (読み直せなければ記録して false)
    private async Task<bool> RefreshAsync<T>(Func<ValueTask<ApiResult<T>>> refresh, string operation, ShellEvent changed)
    {
        var result = await refresh().ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            log.WarnApiFailed(operation, result.Status, result.ErrorCode);
            return false;
        }

        await Navigator.NotifyAsync(changed).ConfigureAwait(true);
        return true;
    }
}
