namespace TableOrder.KitchenApp.Shell;

// 通知の受け手。チケットの通知で一覧を読み直し (続けて届いたら読み直しを 1 回にまとめる)、新しいチケットを音で知らせる
// 品切れは通知の中身で替える。店舗の設定の版が替わったとき、この端末を替えたとき、通知を追いかけられないとき、トークンを断られたときは起動からやり直す
// 同じ通知が 2 回届くことがあるので、扱った seq 以前の通知は捨てる (seq は接続の中で数え、つなぎ直したら数え直す)
// 起動で今の状態を読み終えるまで (Ready の前) に届いた通知は、読んだ状態に入っていないこともあるのでためておき、読み終えたあとに届いた順に扱う
// (品切れと店舗は中身を当て直し、チケットは読み直す。起動からやり直す知らせだけはすぐに扱う)
public sealed class KitchenEventReceiver : IDisposable
{
    // 続けて届いたチケットの音を 1 回にまとめる間
    private static readonly TimeSpan ChimeInterval = TimeSpan.FromSeconds(3);

    // チケットを読み直せなかったときに、もう一度読み直すまでの間 (次のチケットの通知を待たない)
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    private readonly ILogger<KitchenEventReceiver> log;

    private readonly NavigationManager navigation;

    private readonly TimeProvider timeProvider;

    private readonly KioskScreen kiosk;

    private readonly Settings settings;

    private readonly StoreState storeState;

    private readonly StockState stockState;

    private readonly IDeviceApi deviceApi;

    private readonly IOrderEvents events;

    private readonly KitchenUsecase kitchenUsecase;

    // 起動で今の状態を読み終えるまでに届いた通知
    private readonly List<OrderEvent> pending = [];

    private bool started;

    private bool ready;

    // seq を数えている接続
    private long? seqConnection;

    private long lastSeq;

    private bool refreshing;

    private bool refreshAgain;

    // 読み直せなかったチケットの読み直しを待っている (重ねて待たない)
    private bool retrying;

    private DateTimeOffset chimedAt;

    // 状態を替えた (表示中の画面が出し直す)
    public event EventHandler? Changed;

    public KitchenEventReceiver(
        ILogger<KitchenEventReceiver> log,
        NavigationManager navigation,
        TimeProvider timeProvider,
        KioskScreen kiosk,
        Settings settings,
        StoreState storeState,
        StockState stockState,
        IDeviceApi deviceApi,
        IOrderEvents events,
        KitchenUsecase kitchenUsecase)
    {
        this.log = log;
        this.navigation = navigation;
        this.timeProvider = timeProvider;
        this.kiosk = kiosk;
        this.settings = settings;
        this.storeState = storeState;
        this.stockState = stockState;
        this.deviceApi = deviceApi;
        this.events = events;
        this.kitchenUsecase = kitchenUsecase;
    }

    public void Dispose()
    {
        if (started)
        {
            events.Received -= OnReceived;
            events.Expired -= OnExpired;
            deviceApi.Denied -= OnDenied;
        }
    }

    // 通知を受け始める (起動で通知につなぐ前に呼ぶ)。今の状態を読み終える (Ready) まで、届いた通知はためる
    // ためていた通知は、これからつないで読む状態に入っているので捨てる (起動をやり直したとき)
    public void Start()
    {
        ready = false;
        pending.Clear();
        if (started)
        {
            return;
        }

        started = true;
        events.Received += OnReceived;
        events.Expired += OnExpired;
        deviceApi.Denied += OnDenied;
    }

    // 起動で今の状態を読み終えた (ここから通知で状態を替える)。読み終えるまでに届いた通知を届いた順に扱う
    public void Ready()
    {
        ready = true;
        var received = pending.ToList();
        pending.Clear();
        foreach (var e in received)
        {
            _ = HandleAsync(e);
        }
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void OnReceived(object? sender, OrderEventArgs e)
    {
        // つなぎ直した接続 (登録し直してほかの店舗につないだこともある) の通知は、seq を数え直す
        // 前の接続でためていた通知は、つなぎ直したあとに読む状態に入っているので捨てる
        if (e.Connection != seqConnection)
        {
            seqConnection = e.Connection;
            lastSeq = 0;
            pending.Clear();
        }

        if (e.Event.Seq <= lastSeq)
        {
            return;
        }

        lastSeq = e.Event.Seq;
        _ = HandleAsync(e.Event);
    }

    private Task HandleAsync(OrderEvent e)
    {
        if (e is DeviceUpdatedEvent device)
        {
            if (device.DeviceId == settings.DeviceId)
            {
                Restart("device.updated");
            }

            return Task.CompletedTask;
        }

        if (!ready)
        {
            pending.Add(e);
            return Task.CompletedTask;
        }

        switch (e)
        {
            case TicketCreatedEvent:
                Chime();
                return RefreshTicketsAsync();
            case TicketUpdatedEvent:
                return RefreshTicketsAsync();
            case StockUpdatedEvent stock:
                stockState.Apply(stock.Items);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case StoreUpdatedEvent store:
                storeState.Update(store.Store);
                if (storeState.IsSettingsChanged)
                {
                    Restart("store.updated");
                }
                else
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
        }

        return Task.CompletedTask;
    }

    // 読み直している間に届いた通知は、読み終えたあとにもう 1 回だけ読み直す
    // 読み直せなかったら、しばらくしてから読み直す (静かな時間帯に、次のチケットの通知まで古い一覧のままにしない)
    private async Task RefreshTicketsAsync()
    {
        if (refreshing)
        {
            refreshAgain = true;
            return;
        }

        refreshing = true;
        bool failed;
        try
        {
            do
            {
                refreshAgain = false;
                var result = await kitchenUsecase.RefreshTicketsAsync();
                failed = !result.IsSuccess;
                if (failed)
                {
                    log.WarnApiFailed(nameof(IKitchenApi.GetTicketsAsync), result.Status, result.ErrorCode);
                }
            }
            while (refreshAgain);
        }
        finally
        {
            refreshing = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        if (failed)
        {
            _ = RetryLaterAsync();
        }
    }

    private async Task RetryLaterAsync()
    {
        if (retrying)
        {
            return;
        }

        retrying = true;
        await Task.Delay(RetryInterval, timeProvider);
        retrying = false;
        await RefreshTicketsAsync();
    }

    private void Chime()
    {
        var now = timeProvider.GetUtcNow();
        if (now - chimedAt < ChimeInterval)
        {
            return;
        }

        chimedAt = now;
        kiosk.Chime();
    }

    //--------------------------------------------------------------------------------
    // Restart
    //--------------------------------------------------------------------------------

    private void OnExpired(object? sender, EventArgs e) => Restart("expired");

    private void OnDenied(object? sender, DeviceDeniedEventArgs e) => Restart("denied");

    // 状態を読み直すために、アプリを読み込み直して起動からやり直す
    private void Restart(string reason)
    {
        log.InfoRestart(reason);
        navigation.NavigateTo(navigation.BaseUri, forceLoad: true);
    }
}
