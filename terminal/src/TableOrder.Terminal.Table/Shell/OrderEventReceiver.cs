namespace TableOrder.Terminal.Table.Shell;

using TableOrder.Terminal.Table.Components;
using TableOrder.Terminal.Table.Modules;

// 注文サーバの通知を受けて状態を替え、表示中の画面に知らせる (画面での扱いは各画面が決める)
// 操作の途中 (Busy) と遷移の間は待ち、終わってから届いた順に渡す (お客様の操作と重ならないように)
// 端末が使えなくなったとき (無効化、テナントの停止) と EMM が接続先を替えたときは、起動からやり直すように知らせる
public sealed class OrderEventReceiver
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly INavigator navigator;

    private readonly IReactiveMessenger messenger;

    private readonly ManagedConfiguration managedConfiguration;

    private readonly IBusyState busyState;

    private readonly Settings settings;

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

            // 来店が閉じたら、お客様の画面で開いているポップアップを先に閉じる (開いている間は画面が Busy のままで、知らせを渡せない)
            // スタッフメニューのポップアップは閉じない
            if ((e is VisitClosedEvent closed) && visitState.IsOpen && (visitState.Id == closed.Visit.Id) &&
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
        }
    }
}
