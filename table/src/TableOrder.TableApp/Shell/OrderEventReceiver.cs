namespace TableOrder.TableApp.Shell;

using TableOrder.TableApp.Modules;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Shell;

// テーブル端末の通知の扱い。来店・店舗・品切れ・注文の通知で状態を替え、表示中の画面に知らせる (画面での扱いは各画面が決める)
// 受け方 (重複を捨てる、操作と遷移の間を待つ、起動からやり直す知らせ) は土台 (OrderEventReceiverBase) が行う
public sealed class OrderEventReceiver : OrderEventReceiverBase
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly StoreState storeState;

    private readonly OrderUsecase orderUsecase;

    public OrderEventReceiver(
        ILogger<OrderEventReceiver> log,
        INavigator navigator,
        IReactiveMessenger messenger,
        ManagedConfiguration managedConfiguration,
        IBusyState busyState,
        Settings settings,
        MenuState menuState,
        VisitState visitState,
        StoreState storeState,
        IDeviceApi deviceApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase,
        OrderUsecase orderUsecase)
        : base(log, navigator, messenger, managedConfiguration, busyState, settings, deviceApi, events, deviceUsecase)
    {
        this.log = log;
        this.menuState = menuState;
        this.visitState = visitState;
        this.storeState = storeState;
        this.orderUsecase = orderUsecase;
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // 来店が閉じたか、ほかのテーブルに移ったら、お客様の画面で開いているポップアップを先に閉じる (開いている間は画面が Busy のままで、知らせを渡せない)
    // スタッフメニューのポップアップは閉じない
    protected override void OnReceived(OrderEvent e)
    {
        var leaving = e switch
        {
            VisitClosedEvent closed => closed.Visit.Id,
            VisitMovedEvent moved => moved.Visit.Id,
            _ => (Guid?)null
        };
        if ((leaving is { } visitId) && visitState.IsOpen && (visitState.Id == visitId) &&
            (Navigator.CurrentViewId is ViewId.Menu or ViewId.Checkout))
        {
            ClosePopups();
        }
    }

    protected override async Task NotifyRestartAsync() =>
        await Navigator.NotifyAsync(ShellEvent.Restart).ConfigureAwait(true);

    // 状態は通知の中身で替えるので、まとめて読み直すものはない
    protected override Task OnDrainedAsync() => Task.CompletedTask;

    protected override async Task ApplyAsync(OrderEvent e)
    {
        switch (e)
        {
            case VisitOpenedEvent opened:
                // 待受で人数を入れて自分で開いた来店なら、開き直さない
                if (!visitState.IsOpen)
                {
                    orderUsecase.OpenVisit(opened.Visit);
                }

                await Navigator.NotifyAsync(ShellEvent.VisitOpened).ConfigureAwait(true);
                break;
            case VisitUpdatedEvent updated:
                // 人数と会計中の変化を状態に入れる (人数は表示中の画面が出し直す)
                if (visitState.IsOpen && (visitState.Id == updated.Visit.Id))
                {
                    visitState.Update(updated.Visit);
                    await Navigator.NotifyAsync(ShellEvent.VisitUpdated).ConfigureAwait(true);
                }

                break;
            case VisitMovedEvent moved:
                // このテーブルから移ったら待受に戻し、このテーブルに移ってきたら注文の画面にする
                if (visitState.IsOpen && (visitState.Id == moved.Visit.Id))
                {
                    await Navigator.NotifyAsync(ShellEvent.VisitMoved).ConfigureAwait(true);
                }
                else if (!visitState.IsOpen)
                {
                    // 移る前の注文が読めなくても注文の画面にする (上限を超える注文はサーバが断る)
                    var orders = await orderUsecase.OpenMovedVisitAsync(moved.Visit).ConfigureAwait(true);
                    if (!orders.IsSuccess)
                    {
                        log.WarnApiFailed(nameof(ITableApi.GetOrdersAsync), orders.Status, orders.ErrorCode);
                    }

                    await Navigator.NotifyAsync(ShellEvent.VisitOpened).ConfigureAwait(true);
                }

                break;
            case VisitClosedEvent closed:
                // 来店を終えるのは画面が行う (お会計はお礼を出してから終える)
                if (visitState.IsOpen && (visitState.Id == closed.Visit.Id))
                {
                    visitState.Update(closed.Visit);
                    await Navigator.NotifyAsync(ShellEvent.VisitClosed).ConfigureAwait(true);
                }

                break;
            case StoreUpdatedEvent updated:
                storeState.Update(updated.Store);
                await Navigator.NotifyAsync(ShellEvent.StoreUpdated).ConfigureAwait(true);
                break;
            case StockUpdatedEvent stock:
                menuState.ApplyStock(stock.Items);
                await Navigator.NotifyAsync(ShellEvent.StockUpdated).ConfigureAwait(true);
                break;
            case OrderCreatedEvent created:
                // ホール端末で代わりに受けた注文も上限のルールに数える (この端末で送った注文は置き換わるだけ)
                if (visitState.IsOpen && (visitState.Id == created.Order.VisitId))
                {
                    visitState.SetOrdered(created.Order);
                }

                break;
            case OrderLinesUpdatedEvent lines:
                // 取り消した明細を上限のルールの数から外す
                if (visitState.IsOpen && (visitState.Id == lines.VisitId))
                {
                    foreach (var order in lines.Orders)
                    {
                        visitState.SetOrdered(order);
                    }
                }

                break;
        }
    }
}
