namespace TableOrder.Terminal.Table.Modules.Staff;

using TableOrder.Client.Mock;
using TableOrder.Terminal.Table.Components;

// スタッフメニュー。ブランドの印の長押しと PIN で入る
// 端末の情報、来店を開く (ハンディのない店)、端末の設定と PIN、専用端末の一時的な解除、モックの操作を置く
public sealed partial class StaffViewModel : AppViewModelBase
{
    private readonly ILogger<StaffViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly KioskManager kiosk;

    private readonly Settings settings;

    private readonly DeviceState deviceState;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly CartState cartState;

    private readonly ITableApi tableApi;

    private readonly IMockOrderControl mock;

    private readonly OrderUsecase orderUsecase;

    public string TableText { get; }

    public string EndpointText { get; }

    // EMM が配っている設定 (接続先、PIN)
    public string ManagedText { get; }

    public string DeviceIdText { get; }

    public string VersionText { get; }

    public bool CanOpenVisit { get; }

    // PIN を EMM が配っているときは端末で変えない
    public bool CanChangePin { get; }

    [ObservableProperty]
    public partial string BatteryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NetworkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KioskText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsReleased { get; set; }

    [ObservableProperty]
    public partial string OfflineText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PaymentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PauseText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastOrderText { get; set; } = string.Empty;

    public string MockHintText { get; }

    public IObserveCommand OpenVisitCommand { get; }

    public IObserveCommand SetupCommand { get; }

    public IObserveCommand ChangePinCommand { get; }

    public IObserveCommand ReleaseCommand { get; }

    public IObserveCommand RestoreCommand { get; }

    public IObserveCommand SystemSettingsCommand { get; }

    public IObserveCommand OfflineCommand { get; }

    public IObserveCommand PaymentCommand { get; }

    public IObserveCommand SellOutCommand { get; }

    public IObserveCommand RestockCommand { get; }

    public IObserveCommand AdvanceCommand { get; }

    public IObserveCommand HallOpenCommand { get; }

    public IObserveCommand RegisterPayCommand { get; }

    public IObserveCommand PauseCommand { get; }

    public IObserveCommand LastOrderCommand { get; }

    public IObserveCommand CloseCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StaffViewModel(
        ILogger<StaffViewModel> log,
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        DeviceInformation deviceInformation,
        KioskManager kiosk,
        Settings settings,
        DeviceState deviceState,
        MenuState menuState,
        VisitState visitState,
        CartState cartState,
        ITableApi tableApi,
        IMockOrderControl mock,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.kiosk = kiosk;
        this.settings = settings;
        this.deviceState = deviceState;
        this.menuState = menuState;
        this.visitState = visitState;
        this.cartState = cartState;
        this.tableApi = tableApi;
        this.mock = mock;
        this.orderUsecase = orderUsecase;

        TableText = ViewHelper.Table(settings.TableNo);
        EndpointText = String.IsNullOrEmpty(settings.ApiEndPoint) ? AppResources.StaffEndpointMock : settings.ApiEndPoint;
        DeviceIdText = deviceInformation.DeviceId;
        VersionText = ViewHelper.Version(appInfo);
        CanOpenVisit = !visitState.IsOpen;
        CanChangePin = !settings.IsStaffPinManaged;
        ManagedText = ManagedNames(settings);
        MockHintText = ViewHelper.Format(AppResources.StaffMockHintFormat, (int)mock.EventDelay.TotalSeconds);

        OpenVisitCommand = MakeAsyncCommand(OpenVisitAsync);
        SetupCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Setup));
        ChangePinCommand = MakeAsyncCommand(ChangePinAsync);
        ReleaseCommand = MakeDelegateCommand(() => ChangeKiosk(kiosk.Release));
        RestoreCommand = MakeDelegateCommand(() => ChangeKiosk(kiosk.Restore));
        SystemSettingsCommand = MakeDelegateCommand(kiosk.OpenSystemSettings);
        OfflineCommand = MakeDelegateCommand(() =>
        {
            mock.Offline = !mock.Offline;
            Refresh();
        });
        PaymentCommand = MakeDelegateCommand(() =>
        {
            mock.FailPayments = !mock.FailPayments;
            Refresh();
        });
        SellOutCommand = MakeAsyncCommand(SellOutAsync);
        RestockCommand = MakeAsyncCommand(RestockAsync);
        AdvanceCommand = MakeAsyncCommand(AdvanceAsync);
        HallOpenCommand = MakeAsyncCommand(HallOpenAsync);
        RegisterPayCommand = MakeAsyncCommand(RegisterPayAsync);
        PauseCommand = MakeDelegateCommand(() =>
        {
            mock.OrderingPaused = !mock.OrderingPaused;
            Refresh();
        });
        LastOrderCommand = MakeDelegateCommand(() =>
        {
            mock.LastOrder = NextLastOrder(mock.LastOrder);
            Refresh();
        });
        CloseCommand = MakeAsyncCommand(CloseAsync);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        Refresh();
        return Task.CompletedTask;
    }

    // 戻るは閉じると同じにする
    protected override Task OnNotifyBackAsync() => CloseAsync();

    // お客様の画面に戻る (来店中なら注文の画面、そうでなければ待受)。品切れなどの表示を合わせるため作り直す
    private async Task CloseAsync() =>
        await Navigator.ForwardAsync(visitState.IsOpen ? ViewId.Menu : ViewId.Standby);

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    private void Refresh()
    {
        var level = deviceState.BatteryChargeLevel;
        var percent = level >= 0 ? (int)Math.Round(level * 100) : 0;
        var charging = deviceState.BatteryState is BatteryState.Charging or BatteryState.Full;
        BatteryText = ViewHelper.Format(charging ? AppResources.StaffBatteryChargingFormat : AppResources.StaffBatteryFormat, percent);
        NetworkText = deviceState.NetworkState.IsConnected() ? AppResources.StaffConnected : AppResources.StaffDisconnected;

        var status = kiosk.GetStatus();
        var state = status.IsReleased ? AppResources.KioskReleased : status.IsLocked ? AppResources.KioskLocked : AppResources.KioskUnlocked;
        KioskText = ViewHelper.Format(AppResources.StaffKioskFormat, ViewHelper.Name(status.Mode), state);
        IsReleased = status.IsReleased;

        OfflineText = mock.Offline ? AppResources.StaffMockOnline : AppResources.StaffMockOffline;
        PaymentText = mock.FailPayments ? AppResources.StaffMockPassPayments : AppResources.StaffMockFailPayments;
        PauseText = mock.OrderingPaused ? AppResources.StaffMockResume : AppResources.StaffMockPause;
        LastOrderText = NextLastOrder(mock.LastOrder) switch
        {
            MockLastOrder.Soon => AppResources.StaffMockLastOrderSoon,
            MockLastOrder.Passed => AppResources.StaffMockLastOrderPassed,
            _ => AppResources.StaffMockLastOrderNone
        };
    }

    private static string ManagedNames(Settings settings)
    {
        var names = new List<string>();
        if (settings.IsApiEndPointManaged)
        {
            names.Add(AppResources.StaffEndpoint);
        }

        if (settings.IsStaffPinManaged)
        {
            names.Add(AppResources.StaffPin);
        }

        return names.Count > 0 ? String.Join(AppResources.ListSeparator, names) : AppResources.StaffManagedNone;
    }

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // ハンディのない店で、スタッフが人数を入れて来店を開く
    private async Task OpenVisitAsync()
    {
        if (await popupNavigator.GuestCountAsync() is not { } guests)
        {
            return;
        }

        var result = await orderUsecase.StartVisitAsync(guests.Adults, guests.Children);
        if (!result.IsSuccess)
        {
            log.WarnApiFailed(nameof(ITableApi.StartVisitAsync), result.Status, result.ErrorCode);
            await popupNavigator.MessageAsync(AppResources.ErrorTitle, ViewHelper.ErrorMessage(result));
            return;
        }

        await Navigator.ForwardAsync(ViewId.Menu);
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    // 新しい PIN を 2 回入れて、合っていれば変える
    private async Task ChangePinAsync()
    {
        if (await popupNavigator.InputPinAsync(AppResources.StaffNewPin) is not { } pin)
        {
            return;
        }

        if (pin.Length != Length.StaffPinDigits)
        {
            await popupNavigator.MessageAsync(AppResources.StaffChangePin, ViewHelper.Format(AppResources.StaffPinLength, Length.StaffPinDigits));
            return;
        }

        if (await popupNavigator.InputPinAsync(AppResources.StaffNewPinAgain) is not { } again)
        {
            return;
        }

        if (again != pin)
        {
            await popupNavigator.MessageAsync(AppResources.StaffChangePin, AppResources.StaffPinMismatch);
            return;
        }

        settings.StaffPin = pin;
        await popupNavigator.MessageAsync(AppResources.StaffChangePin, AppResources.StaffPinChanged);
    }

    private void ChangeKiosk(Action action)
    {
        action();
        Refresh();
    }

    //--------------------------------------------------------------------------------
    // Mock
    //--------------------------------------------------------------------------------

    private async Task SellOutAsync()
    {
        var itemIds = cartState.Lines.Select(static x => x.ItemId).Distinct().ToList();
        if (itemIds.Count == 0)
        {
            await popupNavigator.MessageAsync(AppResources.StaffMock, AppResources.StaffMockCartEmpty);
            return;
        }

        mock.SellOut(itemIds);
        await ReloadStockAsync();
        await popupNavigator.MessageAsync(AppResources.StaffMock, AppResources.StaffMockSoldOutDone);
    }

    private async Task RestockAsync()
    {
        mock.Restock();
        await ReloadStockAsync();
        await popupNavigator.MessageAsync(AppResources.StaffMock, AppResources.StaffMockRestockDone);
    }

    private async Task AdvanceAsync()
    {
        mock.AdvanceOrders();
        await popupNavigator.MessageAsync(AppResources.StaffMock, AppResources.StaffMockAdvanceDone);
    }

    // ホール端末で来店を開いたことにする (待受に戻ると、知らせを受けて注文の画面になる)
    private async Task HallOpenAsync()
    {
        if (await popupNavigator.GuestCountAsync() is not { } guests)
        {
            return;
        }

        var message = mock.OpenVisit(guests.Adults, guests.Children)
            ? ViewHelper.Format(AppResources.StaffMockHallOpenFormat, (int)mock.EventDelay.TotalSeconds)
            : AppResources.StaffVisitOpen;
        await popupNavigator.MessageAsync(AppResources.StaffMock, message);
    }

    // レジで払い終えたことにする (注文の画面に戻ると、知らせを受けて待受に戻る)
    private async Task RegisterPayAsync()
    {
        var message = mock.CloseVisit()
            ? ViewHelper.Format(AppResources.StaffMockRegisterPayFormat, (int)mock.EventDelay.TotalSeconds)
            : AppResources.StaffMockNoVisit;
        await popupNavigator.MessageAsync(AppResources.StaffMock, message);
    }

    // ラストオーダーは なし → まもなく → 過ぎた の順に替える
    private static MockLastOrder NextLastOrder(MockLastOrder current) =>
        current switch
        {
            MockLastOrder.None => MockLastOrder.Soon,
            MockLastOrder.Soon => MockLastOrder.Passed,
            _ => MockLastOrder.None
        };

    // 品切れの表示を合わせる (注文の画面は閉じるときに作り直すので、そこで反映される)
    private async Task ReloadStockAsync()
    {
        var result = await tableApi.GetStockAsync();
        if (result.Content is { } stock)
        {
            menuState.UpdateStock(stock);
        }
        else
        {
            log.WarnApiFailed(nameof(ITableApi.GetStockAsync), result.Status, result.ErrorCode);
        }
    }
}
