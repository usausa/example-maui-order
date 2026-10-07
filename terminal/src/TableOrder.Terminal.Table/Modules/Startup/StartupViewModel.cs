namespace TableOrder.Terminal.Table.Modules.Startup;

// 起動の準備 (端末の登録、トークン、端末と店舗の設定・メニュー・品切れ・今の来店の読み込み) を進み具合を出しながら行う
// 登録していなければ EMM の登録トークンで登録し、なければ端末の設定へ進む。来店があれば注文の画面へ、なければ待受へ進む
// 失敗したとき (通信できない、テーブルの割り当て待ち、テナントの停止) は、しばらくごとにやり直す
public sealed partial class StartupViewModel : AppViewModelBase
{
    // 段階の間を少し空け、進み具合が読めるようにする
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(300);

    // 失敗したときにやり直すまでの時間
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<StartupViewModel> log;

    private readonly Settings settings;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly StoreState storeState;

    private readonly IDeviceApi deviceApi;

    private readonly ITableApi tableApi;

    private readonly DeviceUsecase deviceUsecase;

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
        IAppInfo appInfo,
        Settings settings,
        MenuState menuState,
        VisitState visitState,
        StoreState storeState,
        IDeviceApi deviceApi,
        ITableApi tableApi,
        DeviceUsecase deviceUsecase)
    {
        this.log = log;
        this.settings = settings;
        this.menuState = menuState;
        this.visitState = visitState;
        this.storeState = storeState;
        this.deviceApi = deviceApi;
        this.tableApi = tableApi;
        this.deviceUsecase = deviceUsecase;

        VersionText = ViewHelper.Version(appInfo);
        RetryHintText = ViewHelper.Format(AppResources.StartupAutoRetryFormat, (int)RetryInterval.TotalSeconds);

        RetryCommand = MakeAsyncCommand(InitializeAsync);
        SetupCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Setup));
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

        // 登録していなければ、EMM が配った登録トークンで登録する (なければ端末の設定で登録する)
        await ReportAsync(0.1, AppResources.StartupStepSettings);
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

        // テーブル端末として登録し、管理画面でテーブルを割り当てるまでは進まない
        if (configContent.Device is not { Kind: DeviceKind.Table } device)
        {
            Fail(AppResources.StartupNotTable);
            return;
        }

        if (device.TableId is null)
        {
            Fail(ViewHelper.Format(AppResources.StartupTableWaitingFormat, device.Name));
            return;
        }

        var store = await tableApi.GetStoreAsync();
        if (store.Content is not { } storeContent)
        {
            Fail(store);
            return;
        }

        storeState.Update(storeContent);

        await ReportAsync(0.55, AppResources.StartupStepMenu);
        var menu = await tableApi.GetMenuAsync();
        if (menu.Content is not { } menuContent)
        {
            Fail(menu);
            return;
        }

        var stock = await tableApi.GetStockAsync();
        if (stock.Content is not { } stockContent)
        {
            Fail(stock);
            return;
        }

        menuState.Update(configContent, menuContent, stockContent);

        // 来店の途中で起動し直したときは、注文の画面に戻す
        await ReportAsync(0.8, AppResources.StartupStepVisit);
        var visit = await tableApi.GetCurrentVisitAsync();
        if (!visit.IsSuccess)
        {
            Fail(visit);
            return;
        }

        if (visit.Content is { } current)
        {
            visitState.Open(current);

            var orders = await tableApi.GetOrdersAsync(current.Id);
            if (orders.Content is not { } ordersContent)
            {
                Fail(orders);
                return;
            }

            visitState.UpdateOrdered(ordersContent.Items);
        }
        else
        {
            visitState.Close();
        }

        await ReportAsync(1.0, AppResources.StartupStepReady);
        await Navigator.ForwardAsync(visitState.IsOpen ? ViewId.Menu : ViewId.Standby);
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
