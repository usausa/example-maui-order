namespace TableOrder.TableApp.Shell;

using TableOrder.TableApp.Components;
using TableOrder.TableApp.Modules;

// 注文サーバの通知を受けて状態を替え、表示中の画面に知らせる (画面での扱いは各画面が決める)
// 操作の途中 (Busy) と遷移の間は待ち、終わってから届いた順に渡す (お客様の操作と重ならないように)
// 端末が使えなくなったとき (無効化、テナントの停止)、管理画面で端末を替えたとき、EMM が接続先を替えたとき、通知を追いかけられなくなったときは、起動からやり直すように知らせる
public sealed class OrderEventReceiver
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly INavigator navigator;

    private readonly IReactiveMessenger messenger;

    private readonly ManagedConfiguration managedConfiguration;

    private readonly IBusyState busyState;

    private readonly Settings settings;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly StoreState storeState;

    private readonly IDeviceApi deviceApi;

    private readonly IOrderEvents events;

    private readonly DeviceUsecase deviceUsecase;

    private readonly OrderUsecase orderUsecase;

    private readonly Queue<OrderEvent> pending = new();

    // seq を数えている端末 (seq は店舗の中の通し番号なので、登録し直したら数え直す)
    private Guid? seqDeviceId;

    private long lastSeq;

    // 起動からやり直すように知らせる (待っている通知より先に渡す)
    private bool restartRequested;

    // EMM が配っている接続先 (替わったら起動からやり直す)
    private string? managedEndPoint;

    private bool delivering;

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
    {
        this.log = log;
        this.navigator = navigator;
        this.messenger = messenger;
        this.managedConfiguration = managedConfiguration;
        this.busyState = busyState;
        this.settings = settings;
        this.menuState = menuState;
        this.visitState = visitState;
        this.storeState = storeState;
        this.deviceApi = deviceApi;
        this.events = events;
        this.deviceUsecase = deviceUsecase;
        this.orderUsecase = orderUsecase;
    }

    public void Start()
    {
        managedEndPoint = managedConfiguration.ApiEndPoint;

        events.Received += HandleReceived;
        events.Expired += HandleExpired;
        deviceApi.Denied += HandleDenied;
        managedConfiguration.Changed += HandleManagedChanged;
        busyState.PropertyChanged += (_, _) => Deliver();
        navigator.ExecutingChanged += (_, _) => Deliver();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // 通知はどのスレッドからも届くので、画面のスレッドで受ける
    private void HandleReceived(object? sender, OrderEventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var e = args.Event;

            // 登録し直した端末 (ほかの店舗のこともある) は、seq を数え直す
            if (settings.DeviceId != seqDeviceId)
            {
                seqDeviceId = settings.DeviceId;
                lastSeq = 0;
            }

            // 同じ通知が 2 回届くことがあるので、seq で重複を捨てる
            if (e.Seq <= lastSeq)
            {
                return;
            }

            lastSeq = e.Seq;
            log.DebugEventReceived(e.GetType().Name, e.Seq, e.OccurredAt);

            // 管理画面で置き場所を替えたか無効にした端末は、起動からやり直して新しいトークンと設定を受け取る (ほかの端末の知らせは使わない)
            if (e is DeviceUpdatedEvent updated)
            {
                if (updated.DeviceId == settings.DeviceId)
                {
                    log.InfoDeviceUpdated();
                    RequestRestart();
                }

                return;
            }

            // 来店が閉じたか、ほかのテーブルに移ったら、お客様の画面で開いているポップアップを先に閉じる (開いている間は画面が Busy のままで、知らせを渡せない)
            // スタッフメニューのポップアップは閉じない
            var leaving = e switch
            {
                VisitClosedEvent closed => closed.Visit.Id,
                VisitMovedEvent moved => moved.Visit.Id,
                _ => (Guid?)null
            };
            if ((leaving is { } visitId) && visitState.IsOpen && (visitState.Id == visitId) &&
                (navigator.CurrentViewId is ViewId.Menu or ViewId.Checkout))
            {
                messenger.Send(new PopupCloseMessage());
            }

            pending.Enqueue(e);
            Deliver();
        });

    //--------------------------------------------------------------------------------
    // Restart
    //--------------------------------------------------------------------------------

    // 無効にされた端末は登録と鍵を消してから起動からやり直す (起動は登録に進む)
    // テナントを止められたときは、起動で止まっていることを出して待つ
    private void HandleDenied(object? sender, DeviceDeniedEventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 登録し直した後に届いた、前の端末の知らせは使わない
            if (args.DeviceId != settings.DeviceId)
            {
                return;
            }

            log.WarnDeviceDenied(args.Reason);
            if (args.Reason == DeviceDenial.Revoked)
            {
                deviceUsecase.Unregister();
            }

            RequestRestart();
        });

    // 抜けた通知を追いかけられなくなったら (長く切れていた)、起動からやり直して今の状態を読み直す
    private void HandleExpired(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            log.WarnEventsExpired();
            RequestRestart();
        });

    // EMM が接続先を替えたら、起動からやり直してつなぎ直す (登録は接続先ごとなので、起動で登録し直す)
    private void HandleManagedChanged(object? sender, EventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (managedConfiguration.ApiEndPoint == managedEndPoint)
            {
                return;
            }

            managedEndPoint = managedConfiguration.ApiEndPoint;
            log.InfoEndPointChanged(settings.ApiEndPoint);
            RequestRestart();
        });

    // 開いているポップアップを閉じてから (開いている間は画面が Busy のままで、知らせを渡せない) 知らせる
    // スタッフメニューのポップアップも閉じる (端末が使えなくなったので、操作を続けさせない)
    private void RequestRestart()
    {
        restartRequested = true;
        messenger.Send(new PopupCloseMessage());
        Deliver();
    }

    //--------------------------------------------------------------------------------
    // Deliver
    //--------------------------------------------------------------------------------

    private void Deliver()
    {
        if (delivering)
        {
            return;
        }

        // 待たずに進めるが、例外はログに残す
        DeliverAsync().ContinueWith(
            t => log.WarnEventDeliveryFailed(t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task DeliverAsync()
    {
        delivering = true;
        try
        {
            while (!busyState.IsBusy && !navigator.Executing)
            {
                // 起動で読み直すので、待っている通知は捨てる
                if (restartRequested)
                {
                    restartRequested = false;
                    pending.Clear();
                    await navigator.NotifyAsync(ShellEvent.Restart).ConfigureAwait(true);
                    continue;
                }

                if (!pending.TryDequeue(out var e))
                {
                    break;
                }

                await ApplyAsync(e).ConfigureAwait(true);
            }
        }
        finally
        {
            delivering = false;
        }
    }

    private async Task ApplyAsync(OrderEvent e)
    {
        switch (e)
        {
            case VisitOpenedEvent opened:
                // 待受で人数を入れて自分で開いた来店なら、開き直さない
                if (!visitState.IsOpen)
                {
                    orderUsecase.OpenVisit(opened.Visit);
                }

                await navigator.NotifyAsync(ShellEvent.VisitOpened).ConfigureAwait(true);
                break;
            case VisitUpdatedEvent updated:
                // 人数と会計中の変化を状態に入れる (人数は表示中の画面が出し直す)
                if (visitState.IsOpen && (visitState.Id == updated.Visit.Id))
                {
                    visitState.Update(updated.Visit);
                    await navigator.NotifyAsync(ShellEvent.VisitUpdated).ConfigureAwait(true);
                }

                break;
            case VisitMovedEvent moved:
                // このテーブルから移ったら待受に戻し、このテーブルに移ってきたら注文の画面にする
                if (visitState.IsOpen && (visitState.Id == moved.Visit.Id))
                {
                    await navigator.NotifyAsync(ShellEvent.VisitMoved).ConfigureAwait(true);
                }
                else if (!visitState.IsOpen)
                {
                    // 移る前の注文が読めなくても注文の画面にする (上限を超える注文はサーバが断る)
                    var orders = await orderUsecase.OpenMovedVisitAsync(moved.Visit).ConfigureAwait(true);
                    if (!orders.IsSuccess)
                    {
                        log.WarnApiFailed(nameof(ITableApi.GetOrdersAsync), orders.Status, orders.ErrorCode);
                    }

                    await navigator.NotifyAsync(ShellEvent.VisitOpened).ConfigureAwait(true);
                }

                break;
            case VisitClosedEvent closed:
                // 来店を終えるのは画面が行う (お会計はお礼を出してから終える)
                if (visitState.IsOpen && (visitState.Id == closed.Visit.Id))
                {
                    visitState.Update(closed.Visit);
                    await navigator.NotifyAsync(ShellEvent.VisitClosed).ConfigureAwait(true);
                }

                break;
            case StoreUpdatedEvent updated:
                storeState.Update(updated.Store);
                await navigator.NotifyAsync(ShellEvent.StoreUpdated).ConfigureAwait(true);
                break;
            case StockUpdatedEvent stock:
                menuState.ApplyStock(stock.Items);
                await navigator.NotifyAsync(ShellEvent.StockUpdated).ConfigureAwait(true);
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
