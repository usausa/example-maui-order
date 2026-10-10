namespace TableOrder.Terminal.Shell;

using TableOrder.Terminal.Components;

// 注文サーバの通知の受け口の土台。通知を受けて重複を捨て、操作の途中 (Busy) と遷移の間は待ち、終わってから届いた順に各アプリの扱い (ApplyAsync) に渡す
// 端末が使えなくなったとき (無効化、テナントの停止)、管理画面で端末を替えたとき、EMM が接続先を替えたとき、通知を追いかけられなくなったときは、起動からやり直すように知らせる (NotifyRestartAsync)
public abstract class OrderEventReceiverBase
{
    private readonly ILogger log;

    private readonly IReactiveMessenger messenger;

    private readonly ManagedConfiguration managedConfiguration;

    private readonly IBusyState busyState;

    private readonly Settings settings;

    private readonly IDeviceApi deviceApi;

    private readonly IOrderEvents events;

    private readonly DeviceUsecase deviceUsecase;

    private readonly Queue<OrderEvent> pending = new();

    // seq を数えている接続 (seq は接続の中で増える。起動でつなぎ直したら、ほかの店舗のこともあるので数え直す)
    private long? seqConnection;

    private long lastSeq;

    // 起動からやり直すように知らせる (待っている通知より先に渡す)
    private bool restartRequested;

    // 扱った通知のあと、待っている通知がなくなったことをまだ知らせていない
    private bool drainPending;

    // EMM が配っている接続先 (替わったら起動からやり直す)
    private string? managedEndPoint;

    private bool delivering;

    protected INavigator Navigator { get; }

    protected OrderEventReceiverBase(
        ILogger log,
        INavigator navigator,
        IReactiveMessenger messenger,
        ManagedConfiguration managedConfiguration,
        IBusyState busyState,
        Settings settings,
        IDeviceApi deviceApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase)
    {
        this.log = log;
        Navigator = navigator;
        this.messenger = messenger;
        this.managedConfiguration = managedConfiguration;
        this.busyState = busyState;
        this.settings = settings;
        this.deviceApi = deviceApi;
        this.events = events;
        this.deviceUsecase = deviceUsecase;
    }

    public void Start()
    {
        managedEndPoint = managedConfiguration.ApiEndPoint;

        events.Received += HandleReceived;
        events.Expired += HandleExpired;
        deviceApi.Denied += HandleDenied;
        managedConfiguration.Changed += HandleManagedChanged;
        busyState.PropertyChanged += (_, _) => Deliver();
        Navigator.ExecutingChanged += (_, _) => Deliver();
    }

    //--------------------------------------------------------------------------------
    // Application
    //--------------------------------------------------------------------------------

    // アプリごとの扱いは abstract にし、使わないアプリも何もしない実装を書く (どの端末で使うかを各アプリに書いておく)

    // 届いた通知を待ちに入れる前に見る (画面のスレッド)。来店が閉じたときにポップアップを先に閉じるなど
    protected abstract void OnReceived(OrderEvent e);

    // 待っていた通知を、操作の途中と遷移の間を避けて届いた順に扱う (状態を替え、表示中の画面に知らせる)
    protected abstract Task ApplyAsync(OrderEvent e);

    // 表示中の画面が起動からやり直す知らせを受けるか (起動と端末の設定の画面は、自分で確かめるので受けない)
    protected abstract bool AcceptsRestart { get; }

    // 起動からやり直すように、表示中の画面に知らせる
    protected abstract Task NotifyRestartAsync();

    // 待っていた通知を扱い終えたとき (まとめて届いた通知のあとに、変わった一覧を 1 回だけ読み直すなど)
    // 操作の途中と遷移の間は、終わってから呼ぶ
    protected abstract Task OnDrainedAsync();

    // 開いているポップアップを閉じる (開いている間は画面が Busy のままで、知らせを渡せない)
    protected void ClosePopups() => messenger.Send(new PopupCloseMessage());

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // 通知はどのスレッドからも届くので、画面のスレッドで受ける
    private void HandleReceived(object? sender, OrderEventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var e = args.Event;

            // つなぎ直した接続 (登録し直してほかの店舗につないだこともある) の通知は、seq を数え直す
            // (登録し直しても、つなぎ直すまでは前の接続の通知が届くので、端末の id では数え直さない)
            if (args.Connection != seqConnection)
            {
                seqConnection = args.Connection;
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

            OnReceived(e);
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

    // 開いているポップアップを閉じてから知らせる
    // スタッフメニューのポップアップも閉じる (端末が使えなくなったので、操作を続けさせない)
    // 知らせを受けない画面 (起動、端末の設定) では閉じない (止めたテナントの知らせが繰り返し届いても、PIN や登録の入力を閉じない)
    private void RequestRestart()
    {
        restartRequested = true;
        if (AcceptsRestart)
        {
            ClosePopups();
        }

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
            while (!busyState.IsBusy && !Navigator.Executing)
            {
                // 起動で読み直すので、待っている通知は捨てる
                // 表示中の画面が受けない (起動の途中、端末の設定) ときは印を残して待ち、受ける画面に移ってから知らせる
                // (起動の途中に届いた端末の変更を捨てると、前に読んだ設定とトークンのまま進む)
                if (restartRequested)
                {
                    if (!AcceptsRestart)
                    {
                        break;
                    }

                    restartRequested = false;
                    drainPending = false;
                    pending.Clear();
                    await NotifyRestartAsync().ConfigureAwait(true);
                    continue;
                }

                if (pending.TryDequeue(out var e))
                {
                    await ApplyAsync(e).ConfigureAwait(true);
                    drainPending = true;
                    continue;
                }

                if (!drainPending)
                {
                    break;
                }

                // 知らせている間に届いた通知は、続けて扱う
                drainPending = false;
                await OnDrainedAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            delivering = false;
        }
    }
}
