namespace TableOrder.HallApp.Modules.Device;

using TableOrder.Terminal.Components;

// 端末の画面。端末の情報を出し、端末の設定と専用端末の一時的な解除は、スタッフの PIN を確かめてから行う
// 閉じたら開いたタブに戻る
public sealed partial class DeviceViewModel : AppViewModelBase
{
    private readonly IPopupNavigator popupNavigator;

    private readonly KioskManager kiosk;

    private readonly DeviceState deviceState;

    private readonly StaffLock staffLock;

    private ViewId returnTab = ViewId.Seats;

    public string StoreNameText { get; }

    // 登録した端末の名前 (端末の設定のもの。管理画面で付け替えられる)
    public string DeviceNameText { get; }

    public string DeviceIdText { get; }

    public string EndpointText { get; }

    // EMM が配っている設定 (接続先、登録トークン)
    public string ManagedText { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial string BatteryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NetworkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KioskText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsReleased { get; set; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand SetupCommand { get; }

    public IObserveCommand ReleaseCommand { get; }

    public IObserveCommand RestoreCommand { get; }

    public IObserveCommand SystemSettingsCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public DeviceViewModel(
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        KioskManager kiosk,
        Settings settings,
        DeviceState deviceState,
        StaffLock staffLock,
        StoreState storeState)
    {
        this.popupNavigator = popupNavigator;
        this.kiosk = kiosk;
        this.deviceState = deviceState;
        this.staffLock = staffLock;

        StoreNameText = ViewHelper.Text(storeState.StoreName);
        DeviceNameText = storeState.DeviceName ?? "--";
        DeviceIdText = settings.DeviceId?.ToString("D") ?? "--";
        EndpointText = settings.ApiEndPoint;
        ManagedText = ManagedNames(settings);
        VersionText = ViewHelper.Version(appInfo);

        CloseCommand = MakeAsyncCommand(CloseAsync);
        SetupCommand = MakeAsyncCommand(OpenSetupAsync);
        ReleaseCommand = MakeAsyncCommand(ReleaseAsync);
        RestoreCommand = MakeDelegateCommand(() => ChangeKiosk(kiosk.Restore));
        SystemSettingsCommand = MakeDelegateCommand(kiosk.OpenSystemSettings);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override Task OnNavigatingToAsync(INavigationContext context)
    {
        returnTab = context.Parameter.GetTab() ?? ViewId.Seats;
        Refresh();
        return Task.CompletedTask;
    }

    // 戻るは閉じると同じにする
    protected override Task OnNotifyBackAsync() => CloseAsync();

    private async Task CloseAsync() =>
        await Navigator.ForwardAsync(returnTab);

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    private void Refresh()
    {
        var level = deviceState.BatteryChargeLevel;
        var percent = level >= 0 ? (int)Math.Round(level * 100) : 0;
        var charging = deviceState.BatteryState is BatteryState.Charging or BatteryState.Full;
        BatteryText = ViewHelper.Format(charging ? AppResources.DeviceBatteryChargingFormat : AppResources.DeviceBatteryFormat, percent);
        NetworkText = deviceState.NetworkState.IsConnected() ? AppResources.DeviceConnected : AppResources.DeviceDisconnected;

        var status = kiosk.GetStatus();
        var state = status.IsReleased ? AppResources.KioskReleased : status.IsLocked ? AppResources.KioskLocked : AppResources.KioskUnlocked;
        KioskText = ViewHelper.Format(AppResources.DeviceKioskFormat, ViewHelper.Name(status.Mode), state);
        IsReleased = status.IsReleased;
    }

    private static string ManagedNames(Settings settings)
    {
        var names = new List<string>();
        if (settings.IsApiEndPointManaged)
        {
            names.Add(AppResources.DeviceEndpoint);
        }

        if (settings.EnrollmentToken is not null)
        {
            names.Add(AppResources.DeviceEnrollmentToken);
        }

        return names.Count > 0 ? String.Join(AppResources.ListSeparator, names) : AppResources.DeviceManagedNone;
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private async Task OpenSetupAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(staffLock))
        {
            await Navigator.ForwardAsync(ViewId.Setup);
        }
    }

    private async Task ReleaseAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(staffLock))
        {
            ChangeKiosk(kiosk.Release);
        }
    }

    private void ChangeKiosk(Action action)
    {
        action();
        Refresh();
    }
}
