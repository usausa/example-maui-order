namespace TableOrder.ReceptionApp.Modules.Setup;

// 端末の設定。接続先 (注文サーバの URL) と、端末の登録 (ペアリングコードを電卓で入れる)。保存したら起動からやり直す
// 登録は接続先ごとに行う。コードを入れずに保存したときは起動で今の登録を確かめ、なければ EMM の登録トークンで登録する
public sealed partial class SetupViewModel : AppViewModelBase
{
    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly DeviceUsecase deviceUsecase;

    public string VersionText { get; }

    [ObservableProperty]
    public partial string ApiEndPoint { get; set; }

    // 接続先を EMM が配っている (入力の代わりに値を出し、保存しない)
    public bool IsEndPointManaged { get; }

    public string EndPointHintText { get; }

    // 今の接続先での登録 (登録済みなら端末の id)
    public string RegistrationText { get; }

    public string RegistrationHintText { get; }

    [ObservableProperty]
    public partial string PairingCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PairingCodeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SaveText { get; set; } = string.Empty;

    public IObserveCommand InputPairingCodeCommand { get; }

    public IObserveCommand SaveCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public SetupViewModel(
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        Settings settings,
        DeviceUsecase deviceUsecase)
    {
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.deviceUsecase = deviceUsecase;

        VersionText = ViewHelper.Version(appInfo);
        ApiEndPoint = settings.ApiEndPoint;
        IsEndPointManaged = settings.IsApiEndPointManaged;
        EndPointHintText = IsEndPointManaged ? AppResources.SetupEndpointManaged : AppResources.SetupEndpointHint;
        RegistrationText = settings.DeviceId is { } deviceId
            ? ViewHelper.Format(AppResources.SetupRegisteredFormat, deviceId)
            : AppResources.SetupNotRegistered;
        RegistrationHintText = settings.IsRegistered
            ? AppResources.SetupRegistrationHint
            : settings.EnrollmentToken is not null ? AppResources.SetupEnrollmentHint : AppResources.SetupRegisterHint;
        UpdatePairingCode(string.Empty);

        InputPairingCodeCommand = MakeAsyncCommand(async () =>
        {
            if (await popupNavigator.InputPairingCodeAsync(PairingCode) is { } value)
            {
                UpdatePairingCode(value);
            }
        });
        SaveCommand = MakeAsyncCommand(SaveAsync, () => (IsEndPointManaged || ApiEndPoints.IsValid(ApiEndPoint)) && (PairingCode.Length is 0 or Length.PairingCodeDigits));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 登録済みなら変えずに起動へ戻る (登録がなければ戻る先がない)
    protected override async Task OnNotifyBackAsync()
    {
        if (settings.IsRegistered)
        {
            await Navigator.ForwardAsync(ViewId.Startup);
        }
    }

    // 登録の失敗はこの画面で出すので、起動からやり直す知らせは受けない
    protected override Task OnRestartAsync() => Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private void UpdatePairingCode(string value)
    {
        PairingCode = value;
        PairingCodeText = String.IsNullOrEmpty(value) ? AppResources.SetupNotEntered : value;
        SaveText = String.IsNullOrEmpty(value) ? AppResources.SetupSave : AppResources.SetupRegister;
    }

    private async Task SaveAsync()
    {
        // EMM が配っている接続先は端末の値に書かない (配られなくなったら端末の値に戻る)
        if (!IsEndPointManaged)
        {
            settings.ApiEndPoint = ApiEndPoint.Trim();
        }

        // 登録できなければ、この画面に残ってコードを入れ直せるようにする
        if (PairingCode.Length > 0)
        {
            var result = await deviceUsecase.PairAsync(PairingCode);
            if (!result.IsSuccess)
            {
                await popupNavigator.MessageAsync(AppResources.SetupRegistration, ViewHelper.ErrorMessage(result));
                return;
            }
        }

        await Navigator.ForwardAsync(ViewId.Startup);
    }
}
