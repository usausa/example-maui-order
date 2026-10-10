namespace TableOrder.ReceptionApp.Modules.Startup;

using TableOrder.Terminal.Components;

// 起動の準備 (端末の登録、トークン、端末の設定、通知の接続、店舗・チェーンのロゴ・空席の読み込み) を進み具合を出しながら行う
// 通知は今の状態を読む前に受け始め、読んでいる間の変化を取りこぼさない
// チェーンと店舗の設定 (色、PIN、言語) はここで入れ、替わったら待受からここに戻って入れ直す
// 接続先がなければ端末の設定へ進む。登録していなければ EMM の登録トークンで登録し、なければ端末の設定へ進む。用意ができたら待受へ進む
// 失敗したとき (通信できない、受付機でない、テナントの停止) は、しばらくごとにやり直す
// 登録トークンを断られたとき (種類の違い、期限切れや取り消し) は、配り直すまで通らないので自動ではやり直さない
public sealed partial class StartupViewModel : AppViewModelBase
{
    // 段階の間を少し空け、進み具合が読めるようにする
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(300);

    // 失敗したときにやり直すまでの時間
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    // チェーンのロゴを受け取るのを待つ時間 (過ぎたら名前の頭の文字で始め、待受に戻ったときに取り直す)
    private static readonly TimeSpan ImageTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<StartupViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly ImageCache imageCache;

    private readonly ThemeManager themeManager;

    private readonly Settings settings;

    private readonly StaffLock staffLock;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    private readonly ReceptionState receptionState;

    private readonly IDeviceApi deviceApi;

    private readonly IReceptionApi receptionApi;

    private readonly IOrderEvents events;

    private readonly DeviceUsecase deviceUsecase;

    private readonly ReceptionUsecase receptionUsecase;

    private CancellationTokenSource? retrying;

    public string VersionText { get; }

    public string RetryHintText { get; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string StepText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    public partial bool IsAutoRetry { get; set; }

    public IObserveCommand RetryCommand { get; }

    public IObserveCommand SetupCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StartupViewModel(
        ILogger<StartupViewModel> log,
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        ImageCache imageCache,
        ThemeManager themeManager,
        Settings settings,
        StaffLock staffLock,
        LanguageState languageState,
        StoreState storeState,
        ReceptionState receptionState,
        IDeviceApi deviceApi,
        IReceptionApi receptionApi,
        IOrderEvents events,
        DeviceUsecase deviceUsecase,
        ReceptionUsecase receptionUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.imageCache = imageCache;
        this.themeManager = themeManager;
        this.settings = settings;
        this.staffLock = staffLock;
        this.languageState = languageState;
        this.storeState = storeState;
        this.receptionState = receptionState;
        this.deviceApi = deviceApi;
        this.receptionApi = receptionApi;
        this.events = events;
        this.deviceUsecase = deviceUsecase;
        this.receptionUsecase = receptionUsecase;

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
        IsAutoRetry = false;

        // 接続先がなければ (EMM も配っていなければ)、端末の設定で入れる
        await ReportAsync(0.1, AppResources.StartupStepSettings);
        if (String.IsNullOrWhiteSpace(settings.ApiEndPoint))
        {
            await Navigator.ForwardAsync(ViewId.Setup);
            return;
        }

        // 登録していなければ、EMM が配った登録トークンで登録する (なければ端末の設定で登録する)
        // 無効にされた端末は、登録トークンが残っていても自分では登録し直さない (端末の設定で登録し直す)
        if (!settings.IsRegistered)
        {
            if (settings.IsRevoked || (settings.EnrollmentToken is not { } token))
            {
                await Navigator.ForwardAsync(ViewId.Setup);
                return;
            }

            await ReportAsync(0.2, AppResources.StartupStepRegister);
            var enrolled = await deviceUsecase.EnrollAsync(token);
            if (!enrolled.IsSuccess)
            {
                // 断られたトークンを送り続けない (登録の流量を使い、ほかの端末の登録を待たせる)
                Fail(enrolled, enrolled.ErrorCode is not (ErrorCodes.DeviceKindMismatch or ErrorCodes.PairingCodeInvalid));
                return;
            }
        }

        // トークンを取り直す (テナントの再開を反映する)。無効にされた端末は登録からやり直す
        await ReportAsync(0.3, AppResources.StartupStepConnect);
        var authenticated = await deviceApi.AuthenticateAsync();
        if (authenticated.ErrorCode == ErrorCodes.DeviceRevoked)
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
        settings.StaffPin = configContent.StaffPin is { } pin ? new StaffPinHash(pin.Iterations, pin.Salt, pin.Hash) : null;
        themeManager.Apply(configContent.Brand.Theme);

        // 受付機として登録していなければ進まない
        if (configContent.Device is not { Kind: DeviceKind.Reception })
        {
            Fail(AppResources.StartupNotReception);
            return;
        }

        var connected = await events.ConnectAsync();
        if (!connected.IsSuccess)
        {
            Fail(connected);
            return;
        }

        await ReportAsync(0.5, AppResources.StartupStepStore);
        var store = await receptionApi.GetStoreAsync();
        if (store.Content is not { } storeContent)
        {
            Fail(store);
            return;
        }

        storeState.Update(storeContent);
        receptionState.Update(configContent);
        languageState.SetAvailable(receptionState.Languages);

        // まだ保存していないチェーンのロゴを受け取る (受け取れなくても止めない)
        await ReportAsync(0.6, AppResources.StartupStepImages);
        using (var timeout = new CancellationTokenSource(ImageTimeout))
        {
            await imageCache.SyncAsync(receptionState.ImageNames, new Progress<double>(x => Progress = 0.6 + (0.2 * x)), timeout.Token);
        }

        await ReportAsync(0.8, AppResources.StartupStepTables);
        var vacancy = await receptionUsecase.RefreshVacancyAsync();
        if (!vacancy.IsSuccess)
        {
            Fail(vacancy);
            return;
        }

        // 端末の言語の設定によらず、店舗の初めの言語から始める
        languageState.Reset();

        await ReportAsync(1.0, AppResources.StartupStepReady);
        await Navigator.ForwardAsync(ViewId.Standby);
    }

    // 起動に失敗した画面はお客様の前に出るので、端末の設定には PIN を確かめて入る
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

    private void Fail<T>(ApiResult<T> result, bool retry = true)
    {
        log.WarnStartupFailed(result.Status, result.ErrorCode);
        Fail(ViewHelper.ErrorMessage(result), retry);
    }

    private void Fail(string message, bool retry = true)
    {
        IsFailed = true;
        IsAutoRetry = retry;
        StepText = message;
        if (!retry)
        {
            return;
        }

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
