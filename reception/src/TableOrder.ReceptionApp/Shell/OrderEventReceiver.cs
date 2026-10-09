namespace TableOrder.ReceptionApp.Shell;

using TableOrder.Terminal.Components;
using TableOrder.Terminal.Shell;

// 受付機の通知の扱い。来店の通知で空席を読み直し、店舗の通知で店舗の今の状態を替えて、表示中の画面に知らせる
// まとめて届いた通知は扱い終えたあと (OnDrainedAsync) に空席を 1 回だけ読み直し、読み直せなかったときは待受がしばらくごとに読み直す
// 受け方 (重複を捨てる、操作と遷移の間を待つ、起動からやり直す知らせ) は土台 (OrderEventReceiverBase) が行う
public sealed class OrderEventReceiver : OrderEventReceiverBase
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly StoreState storeState;

    private readonly ReceptionUsecase receptionUsecase;

    // 空席を読み直す
    private bool vacancyChanged;

    public OrderEventReceiver(
        ILogger<OrderEventReceiver> log,
        INavigator navigator,
        IReactiveMessenger messenger,
        ManagedConfiguration managedConfiguration,
        IBusyState busyState,
        Settings settings,
        StoreState storeState,
        IDeviceApi deviceApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase,
        ReceptionUsecase receptionUsecase)
        : base(log, navigator, messenger, managedConfiguration, busyState, settings, deviceApi, events, deviceUsecase)
    {
        this.log = log;
        this.storeState = storeState;
        this.receptionUsecase = receptionUsecase;
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // 空席を読み直す印は届いたときに付ける (読み直すのは、待っていた通知を扱い終えたとき)
    protected override void OnReceived(OrderEvent e)
    {
        if (e is VisitOpenedEvent or VisitMovedEvent or VisitClosedEvent)
        {
            vacancyChanged = true;
        }
    }

    // 起動で空席を読み直すので、印は消す
    protected override async Task NotifyRestartAsync()
    {
        vacancyChanged = false;
        await Navigator.NotifyAsync(ShellEvent.Restart).ConfigureAwait(true);
    }

    // 店舗は通知の中身で替える (来店の通知は印を付けたので、ここでは何もしない)
    protected override async Task ApplyAsync(OrderEvent e)
    {
        if (e is StoreUpdatedEvent updated)
        {
            storeState.Update(updated.Store);
            await Navigator.NotifyAsync(ShellEvent.StoreUpdated).ConfigureAwait(true);
        }
    }

    // 読み直しを待つ間に届いた通知 (OnReceived) の印を消さないように、読み直す前に印を下ろす
    // 読み直せなかったときは、読み直せなかった印 (ReceptionState) を見て待受が読み直す
    protected override async Task OnDrainedAsync()
    {
        if (!vacancyChanged)
        {
            return;
        }

        vacancyChanged = false;
        var result = await receptionUsecase.RefreshVacancyAsync().ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            log.WarnApiFailed(nameof(IReceptionApi.GetTablesAsync), result.Status, result.ErrorCode);
            return;
        }

        await Navigator.NotifyAsync(ShellEvent.VacancyChanged).ConfigureAwait(true);
    }
}
