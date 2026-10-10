namespace TableOrder.HallApp.Shell;

using TableOrder.HallApp.Modules;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Shell;

// ホール端末の通知の扱い。店舗と品切れは通知の中身で替え、席・呼び出し・提供は変わった一覧を読み直して、表示中の画面に知らせる
// まとめて届いた通知は扱い終えたあと (OnDrainedAsync) に一覧を 1 回だけ読み直し、読み直せなかった一覧は次の通知のあとに読み直す
// 新しい呼び出しは、届いたときに音と振動で知らせる (操作の途中とポップアップを開いている間も待たない)
// 受け方 (重複を捨てる、操作と遷移の間を待つ、起動からやり直す知らせ) は土台 (OrderEventReceiverBase) が行う
public sealed class OrderEventReceiver : OrderEventReceiverBase
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly CallAlert callAlert;

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
        CallAlert callAlert,
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
        this.callAlert = callAlert;
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
            case VisitOpenedEvent or VisitUpdatedEvent:
                tablesChanged = true;
                break;
            case OrderCreatedEvent:
                // 持ち場のない品 (スタッフが運ぶ品) は、注文を受けたときにできあがりになる (届く通知は order.created だけ)
                tablesChanged = true;
                servingChanged = true;
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
            case CallCreatedEvent:
                tablesChanged = true;
                callsChanged = true;
                callAlert.Alert();
                break;
            case CallUpdatedEvent:
                tablesChanged = true;
                callsChanged = true;
                break;
        }
    }

    // 起動ですべて読み直すので、読み直す一覧の印は消す
    // 起動と端末の設定の画面は、自分で確かめるので受けない
    protected override bool AcceptsRestart => Navigator.CurrentViewId is not (ViewId.Startup or ViewId.Setup);

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
                if (storeState.IsSettingsChanged)
                {
                    log.InfoSettingsChanged(updated.Store.SettingsVersion);
                }

                await Navigator.NotifyAsync(ShellEvent.StoreChanged).ConfigureAwait(true);
                break;
            case StockUpdatedEvent stock:
                menuState.ApplyStock(stock.Items);
                await Navigator.NotifyAsync(ShellEvent.StockChanged).ConfigureAwait(true);
                break;
        }
    }

    // 読み直しを待つ間に届いた通知 (OnReceived) の印を消さないように、読み直す前に印を下ろし、読み直せなかったら戻す
    // (戻すときは読み直しを待ってから印を読む。待つ前に読んだ値で書くと、待つ間に付いた印を消す)
    protected override async Task OnDrainedAsync()
    {
        if (tablesChanged)
        {
            tablesChanged = false;
            var refreshed = await RefreshAsync(hallUsecase.RefreshTablesAsync, nameof(IHallApi.GetTablesAsync), ShellEvent.TablesChanged).ConfigureAwait(true);
            tablesChanged |= !refreshed;
        }

        if (callsChanged)
        {
            callsChanged = false;
            var refreshed = await RefreshAsync(hallUsecase.RefreshCallsAsync, nameof(IHallApi.GetCallsAsync), ShellEvent.CallsChanged).ConfigureAwait(true);
            callsChanged |= !refreshed;
        }

        if (servingChanged)
        {
            servingChanged = false;
            var refreshed = await RefreshAsync(hallUsecase.RefreshServingAsync, nameof(IHallApi.GetServingAsync), ShellEvent.ServingChanged).ConfigureAwait(true);
            servingChanged |= !refreshed;
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
