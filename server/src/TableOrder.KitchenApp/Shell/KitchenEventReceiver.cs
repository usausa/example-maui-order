namespace TableOrder.KitchenApp.Shell;

// 通知の受け手。チケットの通知で一覧を読み直し (続けて届いたら読み直しを 1 回にまとめる)、新しいチケットを音で知らせる
// 品切れは通知の中身で替える。店舗の設定の版が替わったとき、この端末を替えたとき、通知を追いかけられないとき、トークンを断られたときは起動からやり直す
// 同じ通知が 2 回届くことがあるので、扱った seq 以前の通知は捨てる
// 起動で今の状態を読み終えるまで (Ready の前) に届いた通知は、起動で読んだ状態に含まれるので扱わない (起動からやり直す知らせだけは扱う)
public sealed class KitchenEventReceiver : IDisposable
{
    // 続けて届いたチケットの音を 1 回にまとめる間
    private static readonly TimeSpan ChimeInterval = TimeSpan.FromSeconds(3);

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

    private bool started;

    private bool ready;

    private long lastSeq;

    private bool refreshing;

    private bool refreshAgain;

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

    // 通知を受け始める (起動で通知につなぐ前に呼ぶ)
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        events.Received += OnReceived;
        events.Expired += OnExpired;
        deviceApi.Denied += OnDenied;
    }

    // 起動で今の状態を読み終えた (ここから通知で状態を替える)
    public void Ready() => ready = true;

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void OnReceived(object? sender, OrderEventArgs e)
    {
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
    private async Task RefreshTicketsAsync()
    {
        if (refreshing)
        {
            refreshAgain = true;
            return;
        }

        refreshing = true;
        try
        {
            do
            {
                refreshAgain = false;
                var result = await kitchenUsecase.RefreshTicketsAsync();
                if (!result.IsSuccess)
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
