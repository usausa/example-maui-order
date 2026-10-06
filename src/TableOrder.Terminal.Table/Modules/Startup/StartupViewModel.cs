namespace TableOrder.Terminal.Table.Modules.Startup;

// 起動の準備 (設定の確認、店舗の設定・メニュー・品切れ・今の来店の読み込み) を進み具合を出しながら行う。
// 設定がなければ設定の画面へ、来店があれば注文の画面へ、なければ待受へ進む
public sealed partial class StartupViewModel : AppViewModelBase
{
    // 段階の間を少し空け、進み具合が読めるようにする
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(300);

    private readonly ILogger<StartupViewModel> log;

    private readonly IOrderApi orderApi;

    private readonly Settings settings;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly StoreState storeState;

    public string VersionText { get; }

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
        IOrderApi orderApi,
        Settings settings,
        MenuState menuState,
        VisitState visitState,
        StoreState storeState)
    {
        this.log = log;
        this.orderApi = orderApi;
        this.settings = settings;
        this.menuState = menuState;
        this.visitState = visitState;
        this.storeState = storeState;

        VersionText = ViewHelper.Version(appInfo);

        RetryCommand = MakeAsyncCommand(InitializeAsync);
        SetupCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Setup));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override async Task OnNavigatedToAsync(INavigationContext context) =>
        await Navigator.PostActionAsync(InitializeAsync);

    // 起動の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Initialize
    //--------------------------------------------------------------------------------

    private async Task InitializeAsync()
    {
        IsFailed = false;

        await ReportAsync(0.1, AppResources.StartupStepSettings);
        if (!settings.IsConfigured)
        {
            await Navigator.ForwardAsync(ViewId.Setup);
            return;
        }

        await ReportAsync(0.3, AppResources.StartupStepConnect);
        var config = await orderApi.GetConfigAsync();
        if (config.Content is not { } configContent)
        {
            Fail(config);
            return;
        }

        var store = await orderApi.GetStoreAsync();
        if (store.Content is not { } storeContent)
        {
            Fail(store);
            return;
        }

        storeState.Update(storeContent);

        await ReportAsync(0.55, AppResources.StartupStepMenu);
        var menu = await orderApi.GetMenuAsync();
        if (menu.Content is not { } menuContent)
        {
            Fail(menu);
            return;
        }

        var stock = await orderApi.GetStockAsync();
        if (stock.Content is not { } stockContent)
        {
            Fail(stock);
            return;
        }

        menuState.Update(configContent, menuContent, stockContent);

        // 来店の途中で起動し直したときは、注文の画面に戻す
        await ReportAsync(0.8, AppResources.StartupStepVisit);
        var visit = await orderApi.GetCurrentVisitAsync();
        if (!visit.IsSuccess)
        {
            Fail(visit);
            return;
        }

        if (visit.Content is { } current)
        {
            visitState.Open(current);

            var orders = await orderApi.GetOrdersAsync(current.Id);
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

        IsFailed = true;
        StepText = ViewHelper.ErrorMessage(result);
    }
}
