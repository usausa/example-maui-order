namespace TableOrder.KitchenApp.Components.Pages;

// 起動の準備 (鍵、トークン、端末の設定、通知の接続、店舗・メニュー・品切れ・チケットの読み込み) を進み具合を出しながら行う
// 通知は今の状態を読む前に受け始め、読んでいる間の変化を取りこぼさない
// 登録していなければ端末の設定へ進む。用意ができたらチケットの画面へ進む
// 失敗したとき (通信できない、キッチン端末でない、テナントの停止) は、しばらくごとにやり直す
public sealed partial class StartupPage : IDisposable
{
    // 段階の間を少し空け、進み具合が読めるようにする
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(300);

    // 失敗したときにやり直すまでの時間
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    // テナントの停止で断られたときにやり直すまでの時間 (再開まで長く続くので、止めている間に要求を送り続けない)
    private static readonly TimeSpan SuspendedRetryInterval = TimeSpan.FromMinutes(5);

    private CancellationTokenSource? retrying;

    private double progress;

    private string stepText = string.Empty;

    private bool isFailed;

    private bool isRunning;

    // 自動でやり直す間隔の案内 (テナントの停止は間を延ばす)
    private string retryHintText = string.Empty;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required ILogger<StartupPage> Log { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required KioskScreen Kiosk { get; set; }

    [Inject]
    public required BrowserDeviceKey DeviceKey { get; set; }

    [Inject]
    public required StartupState StartupState { get; set; }

    [Inject]
    public required Settings Settings { get; set; }

    [Inject]
    public required StoreState StoreState { get; set; }

    [Inject]
    public required IDeviceApi DeviceApi { get; set; }

    [Inject]
    public required IKitchenApi KitchenApi { get; set; }

    [Inject]
    public required IOrderEvents Events { get; set; }

    [Inject]
    public required KitchenUsecase KitchenUsecase { get; set; }

    [Inject]
    public required KitchenEventReceiver Receiver { get; set; }

    [Inject]
    public required StatusReporter StatusReporter { get; set; }

    private string ProgressWidth => FormattableString.Invariant($"{progress * 100:0}%");

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    public void Dispose() => StopRetry();

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        firstRender ? InitializeAsync() : Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Initialize
    //--------------------------------------------------------------------------------

    private async Task InitializeAsync()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        try
        {
            await RunAsync();
        }
        finally
        {
            isRunning = false;
            StateHasChanged();
        }
    }

    private async Task RunAsync()
    {
        StopRetry();
        isFailed = false;

        // 鍵を作れないブラウザ (HTTPS でない) は登録もできないので、知らせて止まる
        await ReportAsync(0.1, AppResources.StartupStepSettings);
        await DeviceKey.InitializeAsync();
        await Kiosk.InitializeAsync();
        if (!DeviceKey.IsAvailable)
        {
            Fail(AppResources.ErrorDeviceKey);
            return;
        }

        if (!Settings.IsRegistered)
        {
            Navigation.NavigateTo("setup");
            return;
        }

        // トークンを取り直す (テナントの再開を反映する)。無効にされた端末は登録からやり直す
        await ReportAsync(0.3, AppResources.StartupStepConnect);
        var authenticated = await DeviceApi.AuthenticateAsync();
        if (authenticated.ErrorCode == ErrorCodes.DeviceRevoked)
        {
            Settings.Unregister();
            Navigation.NavigateTo("setup");
            return;
        }

        if (!authenticated.IsSuccess)
        {
            Fail(authenticated);
            return;
        }

        var config = await DeviceApi.GetConfigAsync();
        if (config.Content is not { } configContent)
        {
            Fail(config);
            return;
        }

        // キッチン端末として登録していなければ進まない
        if (configContent.Device is not { Kind: DeviceKind.Kitchen })
        {
            Fail(AppResources.StartupNotKitchen);
            return;
        }

        StoreState.Update(configContent);

        Receiver.Start();
        var connected = await Events.ConnectAsync();
        if (!connected.IsSuccess)
        {
            Fail(connected);
            return;
        }

        await ReportAsync(0.5, AppResources.StartupStepStore);
        var store = await KitchenApi.GetStoreAsync();
        if (store.Content is not { } storeContent)
        {
            Fail(store);
            return;
        }

        StoreState.Update(storeContent);

        var menu = await KitchenApi.GetMenuAsync();
        if (menu.Content is not { } menuContent)
        {
            Fail(menu);
            return;
        }

        StoreState.Update(menuContent);

        var stock = await KitchenUsecase.RefreshStockAsync();
        if (!stock.IsSuccess)
        {
            Fail(stock);
            return;
        }

        await ReportAsync(0.8, AppResources.StartupStepTickets);
        var tickets = await KitchenUsecase.RefreshTicketsAsync();
        if (!tickets.IsSuccess)
        {
            Fail(tickets);
            return;
        }

        Receiver.Ready();
        StatusReporter.Start();
        StartupState.Complete();

        await ReportAsync(1.0, AppResources.StartupStepReady);
        Navigation.NavigateTo("tickets", replace: true);
    }

    private void OpenSetup() => Navigation.NavigateTo("setup");

    private Task ReportAsync(double value, string text)
    {
        progress = value;
        stepText = text;
        StateHasChanged();
        return Task.Delay(StepInterval);
    }

    private void Fail<T>(ApiResult<T> result)
    {
        Log.WarnStartupFailed(result.Status, result.ErrorCode);
        Fail(ViewHelper.ErrorMessage(result), result.ErrorCode == ErrorCodes.TenantSuspended ? SuspendedRetryInterval : RetryInterval);
    }

    private void Fail(string message, TimeSpan? interval = null)
    {
        isFailed = true;
        stepText = message;

        var wait = interval ?? RetryInterval;
        retryHintText = RetryHint(wait);
        retrying = new CancellationTokenSource();
        _ = RetryLaterAsync(wait, retrying.Token);
    }

    // 自動でやり直す間隔の案内 (1 分からは分で出す)
    private static string RetryHint(TimeSpan interval) =>
        interval >= TimeSpan.FromMinutes(1)
            ? ViewHelper.Format(AppResources.StartupAutoRetryMinutesFormat, (int)interval.TotalMinutes)
            : ViewHelper.Format(AppResources.StartupAutoRetryFormat, (int)interval.TotalSeconds);

    //--------------------------------------------------------------------------------
    // Retry
    //--------------------------------------------------------------------------------

    // しばらくしたらやり直す (店の端末は人が触らずに戻れるように)
    private async Task RetryLaterAsync(TimeSpan interval, CancellationToken token)
    {
        try
        {
            await Task.Delay(interval, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(InitializeAsync);
    }

    private void StopRetry()
    {
        retrying?.Cancel();
        retrying?.Dispose();
        retrying = null;
    }
}
