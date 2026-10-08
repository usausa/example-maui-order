namespace TableOrder.HallApp.Modules.Startup;

// 起動の準備 (端末の登録、トークン、端末の設定、通知の接続、店舗・メニュー・品切れ・席・呼び出し・提供の読み込み) を進み具合を出しながら行う
// 通知は今の状態を読む前に受け始め、読んでいる間の変化を取りこぼさない
// 接続先がなければ端末の設定へ進む。登録していなければ EMM の登録トークンで登録し、なければ端末の設定へ進む。読み終えたら席のタブへ進む
// 失敗したとき (通信できない、ホール端末でない、テナントの停止) は、しばらくごとにやり直す
public sealed partial class StartupViewModel : AppViewModelBase
{
    // 段階の間を少し空け、進み具合が読めるようにする
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(300);

    // 失敗したときにやり直すまでの時間
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<StartupViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly StaffLock staffLock;

    private readonly StoreState storeState;

    private readonly MenuState menuState;

    private readonly IDeviceApi deviceApi;

    private readonly IHallApi hallApi;

    private readonly IOrderEvents events;

    private readonly DeviceUsecase deviceUsecase;

    private readonly HallUsecase hallUsecase;

    private CancellationTokenSource? retrying;

    public string VersionText { get; }

    public string RetryHintText { get; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string StepText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    public IObserveCommand RetryCommand { get; }

    public IObserveCommand SetupCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StartupViewModel(
        ILogger<StartupViewModel> log,
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        Settings settings,
        StaffLock staffLock,
        StoreState storeState,
        MenuState menuState,
        IDeviceApi deviceApi,
        IHallApi hallApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase,
        HallUsecase hallUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.staffLock = staffLock;
        this.storeState = storeState;
        this.menuState = menuState;
        this.deviceApi = deviceApi;
        this.hallApi = hallApi;
        this.events = events;
        this.deviceUsecase = deviceUsecase;
        this.hallUsecase = hallUsecase;

        VersionText = ViewHelper.Version(appInfo);
        RetryHintText = ViewHelper.Format(AppResources.StartupAutoRetryFormat, (int)RetryInterval.TotalSeconds);

        RetryCommand = MakeAsyncCommand(InitializeAsync);
        SetupCommand = MakeAsyncCommand(OpenSetupAsync);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRetry();
        }

        base.Dispose(disposing);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override async Task OnNavigatedToAsync(INavigationContext context) =>
        await Navigator.PostActionAsync(InitializeAsync);

    // 起動の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    // 起動の画面は自分で確かめ直すので、起動からやり直す知らせは受けない
    protected override Task OnRestartAsync() => Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Initialize
    //--------------------------------------------------------------------------------

    private async Task InitializeAsync()
    {
        StopRetry();
        IsFailed = false;

        // 接続先がなければ (EMM も配っていなければ)、端末の設定で入れる
        await ReportAsync(0.1, AppResources.StartupStepSettings);
        if (String.IsNullOrWhiteSpace(settings.ApiEndPoint))
        {
            await Navigator.ForwardAsync(ViewId.Setup);
            return;
        }

        // 登録していなければ、EMM が配った登録トークンで登録する (なければ端末の設定で登録する)
        if (!settings.IsRegistered)
        {
            if (settings.EnrollmentToken is not { } token)
            {
                await Navigator.ForwardAsync(ViewId.Setup);
                return;
            }

            await ReportAsync(0.2, AppResources.StartupStepRegister);
            var enrolled = await deviceUsecase.EnrollAsync(token);
            if (!enrolled.IsSuccess)
            {
                Fail(enrolled);
                return;
            }
        }

        // トークンを取り直す (置き場所の変更とテナントの再開を反映する)。無効にされた端末は登録からやり直す
        await ReportAsync(0.3, AppResources.StartupStepConnect);
        var authenticated = await deviceApi.AuthenticateAsync();
        if (authenticated.ErrorCode == "DEVICE_REVOKED")
        {
            deviceUsecase.Unregister();
            await Navigator.ForwardAsync(ViewId.Startup);
            return;
        }

        if (!authenticated.IsSuccess)
        {
            Fail(authenticated);
            return;
        }

        var config = await deviceApi.GetConfigAsync();
        if (config.Content is not { } configContent)
        {
            Fail(config);
            return;
        }

        // PIN はサーバにつながらないときにも確かめられるように、登録と一緒に保存する
        settings.StaffPin = new StaffPinHash(configContent.StaffPin.Iterations, configContent.StaffPin.Salt, configContent.StaffPin.Hash);

        // ホール端末として登録されていなければ進まない
        if (configContent.Device is not { Kind: DeviceKind.Hall })
        {
            Fail(AppResources.StartupNotHall);
            return;
        }

        var connected = await events.ConnectAsync();
        if (!connected.IsSuccess)
        {
            Fail(connected);
            return;
        }

        var store = await hallApi.GetStoreAsync();
        if (store.Content is not { } storeContent)
        {
            Fail(store);
            return;
        }

        storeState.Update(configContent, storeContent);

        await ReportAsync(0.5, AppResources.StartupStepMenu);
        var menu = await hallApi.GetMenuAsync();
        if (menu.Content is not { } menuContent)
        {
            Fail(menu);
            return;
        }

        var stock = await hallApi.GetStockAsync();
        if (stock.Content is not { } stockContent)
        {
            Fail(stock);
            return;
        }

        menuState.Update(menuContent, stockContent);

        await ReportAsync(0.7, AppResources.StartupStepFloor);
        var tables = await hallUsecase.RefreshTablesAsync();
        if (!tables.IsSuccess)
        {
            Fail(tables);
            return;
        }

        var calls = await hallUsecase.RefreshCallsAsync();
        if (!calls.IsSuccess)
        {
            Fail(calls);
            return;
        }

        var serving = await hallUsecase.RefreshServingAsync();
        if (!serving.IsSuccess)
        {
            Fail(serving);
            return;
        }

        await ReportAsync(1.0, AppResources.StartupStepReady);
        await Navigator.ForwardAsync(ViewId.Seats);
    }

    // 起動に失敗した画面は誰でも触れるので、端末の設定には PIN を確かめて入る
    private async Task OpenSetupAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(staffLock))
        {
            await Navigator.ForwardAsync(ViewId.Setup);
        }
    }

    private Task ReportAsync(double progress, string text)
    {
        Progress = progress;
        StepText = text;
        return Task.Delay(StepInterval);
    }

    private void Fail<T>(ApiResult<T> result)
    {
        log.WarnStartupFailed(result.Status, result.ErrorCode);
        Fail(ViewHelper.ErrorMessage(result));
    }

    private void Fail(string message)
    {
        IsFailed = true;
        StepText = message;

        retrying = new CancellationTokenSource();
        _ = RetryLaterAsync(retrying.Token);
    }

    //--------------------------------------------------------------------------------
    // Retry
    //--------------------------------------------------------------------------------

    // しばらくしたらやり直す。操作の途中 (もう一度試す、端末の設定) なら、その次の機会にやり直す
    private async Task RetryLaterAsync(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await Task.Delay(RetryInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!BusyState.IsBusy)
            {
                break;
            }
        }

        using (BusyState.Begin())
        {
            await InitializeAsync();
        }
    }

    private void StopRetry()
    {
        retrying?.Cancel();
        retrying?.Dispose();
        retrying = null;
    }
}
