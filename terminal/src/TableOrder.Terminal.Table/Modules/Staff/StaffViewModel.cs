namespace TableOrder.Terminal.Table.Modules.Staff;

using TableOrder.Terminal.Table.Components;

// スタッフメニュー。ブランドの印の長押しと PIN で入る
// 端末の情報、端末の設定、専用端末の一時的な解除を置く (PIN は管理画面の店舗の設定で替える。来店はスタッフがホール端末や管理画面の案内で開く)
public sealed partial class StaffViewModel : AppViewModelBase
{
    private readonly KioskManager kiosk;

    private readonly DeviceState deviceState;

    private readonly VisitState visitState;

    public string TableText { get; }

    // 登録した端末の名前 (端末の設定のもの。管理画面で付け替えられる)
    public string DeviceNameText { get; }

    public string EndpointText { get; }

    // EMM が配っている設定 (接続先、登録トークン)
    public string ManagedText { get; }

    public string DeviceIdText { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial string BatteryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NetworkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KioskText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsReleased { get; set; }

    public IObserveCommand SetupCommand { get; }

    public IObserveCommand ReleaseCommand { get; }

    public IObserveCommand RestoreCommand { get; }

    public IObserveCommand SystemSettingsCommand { get; }

    public IObserveCommand CloseCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StaffViewModel(
        IAppInfo appInfo,
        KioskManager kiosk,
        Settings settings,
        DeviceState deviceState,
        MenuState menuState,
        VisitState visitState)
    {
        this.kiosk = kiosk;
        this.deviceState = deviceState;
        this.visitState = visitState;

        TableText = ViewHelper.Table(menuState.TableName);
        DeviceNameText = menuState.Config.Device?.Name ?? "--";
        EndpointText = settings.ApiEndPoint;
        DeviceIdText = settings.DeviceId?.ToString("D") ?? "--";
        VersionText = ViewHelper.Version(appInfo);
        ManagedText = ManagedNames(settings);

        SetupCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Setup));
        ReleaseCommand = MakeDelegateCommand(() => ChangeKiosk(kiosk.Release));
        RestoreCommand = MakeDelegateCommand(() => ChangeKiosk(kiosk.Restore));
        SystemSettingsCommand = MakeDelegateCommand(kiosk.OpenSystemSettings);
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
    }

    private static string ManagedNames(Settings settings)
    {
        var names = new List<string>();
        if (settings.IsApiEndPointManaged)
        {
            names.Add(AppResources.StaffEndpoint);
        }

        if (settings.EnrollmentToken is not null)
        {
            names.Add(AppResources.StaffEnrollmentToken);
        }

        return names.Count > 0 ? String.Join(AppResources.ListSeparator, names) : AppResources.StaffManagedNone;
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    private void ChangeKiosk(Action action)
    {
        action();
        Refresh();
    }
}
