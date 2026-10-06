namespace TableOrder.Terminal.Table.Modules.Standby;

// 待受。来店はホール端末で開く想定だが、ホール端末ができるまではお客様が人数を入れて始める (selfStart)
public sealed class StandbyViewModel : AppViewModelBase
{
    private readonly ILogger<StandbyViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly LanguageState languageState;

    private readonly OrderUsecase orderUsecase;

    public string Message { get; }

    public string TableText { get; }

    public string LanguageText { get; }

    public bool CanStart { get; }

    public IObserveCommand StartCommand { get; }

    public IObserveCommand LanguageCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StandbyViewModel(
        ILogger<StandbyViewModel> log,
        IPopupNavigator popupNavigator,
        Settings settings,
        MenuState menuState,
        LanguageState languageState,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.languageState = languageState;
        this.orderUsecase = orderUsecase;

        CanStart = menuState.Config.OrderRules.SelfStart;
        Message = CanStart ? AppResources.StandbyMessage : AppResources.StandbyWaiting;
        TableText = ViewHelper.Format(AppResources.TableFormat, ViewHelper.Table(settings.TableNo));
        LanguageText = ViewHelper.SwitchName(languageState.Current);

        StartCommand = MakeAsyncCommand(StartAsync, () => CanStart);
        LanguageCommand = MakeAsyncCommand(SwitchLanguageAsync);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // お客様の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private async Task StartAsync()
    {
        if (await popupNavigator.GuestCountAsync() is not { } guests)
        {
            return;
        }

        var result = await orderUsecase.StartVisitAsync(guests.Adults, guests.Children);
        if (!result.IsSuccess)
        {
            log.WarnApiFailed(nameof(IOrderApi.StartVisitAsync), result.Status, result.ErrorCode);
            await popupNavigator.MessageAsync(AppResources.ErrorTitle, ViewHelper.ErrorMessage(result));
            return;
        }

        await Navigator.ForwardAsync(ViewId.Menu);
    }

    // 文言を引き直すために画面を作り直す
    private async Task SwitchLanguageAsync()
    {
        languageState.Change(languageState.Current == Language.Japanese ? Language.English : Language.Japanese);
        await Navigator.ForwardAsync(ViewId.Standby);
    }
}
