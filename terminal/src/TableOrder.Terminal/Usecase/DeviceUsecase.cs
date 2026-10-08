namespace TableOrder.Terminal.Usecase;

using TableOrder.Terminal.Components;

// 端末の登録 (ペアリングコード、EMM の登録トークン) と解除、状態の報告
// 登録した端末の id は今の接続先と組にして設定に持つ。鍵は登録し直しても使い回し、無効にされたときに作り直す
public sealed class DeviceUsecase
{
    // アプリの端末の種類と違うコードで登録しようとした (この端末は登録しない。サーバに残った端末は管理画面で無効にする)
    public const string KindMismatch = "DEVICE_KIND_MISMATCH";

    // 管理画面で見分けられるように、端末の名前に付ける端末ごとの値の桁数
    private const int SuffixLength = 4;

    private readonly ILogger<DeviceUsecase> log;

    private readonly IAppInfo appInfo;

    private readonly IDeviceInfo deviceInfo;

    private readonly DeviceInformation deviceInformation;

    private readonly TerminalOptions options;

    private readonly Settings settings;

    private readonly DeviceState deviceState;

    private readonly IDeviceApi deviceApi;

    public DeviceUsecase(
        ILogger<DeviceUsecase> log,
        IAppInfo appInfo,
        IDeviceInfo deviceInfo,
        DeviceInformation deviceInformation,
        TerminalOptions options,
        Settings settings,
        DeviceState deviceState,
        IDeviceApi deviceApi)
    {
        this.log = log;
        this.appInfo = appInfo;
        this.deviceInfo = deviceInfo;
        this.deviceInformation = deviceInformation;
        this.options = options;
        this.settings = settings;
        this.deviceState = deviceState;
        this.deviceApi = deviceApi;
    }

    //--------------------------------------------------------------------------------
    // Registration
    //--------------------------------------------------------------------------------

    // 管理画面で出したペアリングコードで登録する (置き場所はコードで決まる)
    public ValueTask<ApiResult<DevicePairResponse>> PairAsync(string pairingCode) =>
        PairAsync(new DevicePairRequest { PairingCode = pairingCode });

    // EMM が配った登録トークンで登録する (置き場所は管理画面で割り当てる)
    public ValueTask<ApiResult<DevicePairResponse>> EnrollAsync(string enrollmentToken) =>
        PairAsync(new DevicePairRequest { EnrollmentToken = enrollmentToken });

    // 無効にされた端末の登録と鍵を消す (登録からやり直す)
    public void Unregister() => settings.Unregister();

    private async ValueTask<ApiResult<DevicePairResponse>> PairAsync(DevicePairRequest request)
    {
        // 鍵を作るのは時間がかかることがあるので、画面のスレッドの外で行う
        var publicKey = await Task.Run(settings.Key.GetPublicKey);
        request.PublicKey = DeviceCredentials.CreatePublicKey(publicKey);
        request.DeviceName = DeviceName();
        request.AppVersion = AppVersion();

        var result = await deviceApi.PairAsync(request);
        if (result.Content is not { } device)
        {
            log.WarnDeviceRegistrationFailed(result.Status, result.ErrorCode);
            return result;
        }

        if (device.Kind != options.Kind)
        {
            log.WarnDeviceRegistrationFailed(ApiStatus.Rejected, KindMismatch);
            return ApiResult.Failure<DevicePairResponse>(ApiStatus.Rejected, KindMismatch);
        }

        settings.Register(device.DeviceId);
        log.InfoDeviceRegistered(device.DeviceId, device.StoreId);
        return result;
    }

    // 端末の名前 (機種の名前と端末ごとの値の末尾。同じ機種が並んでも管理画面で見分けられるように)
    private string DeviceName()
    {
        var id = deviceInformation.DeviceId;
        var name = id.Length >= SuffixLength
            ? $"{deviceInfo.Name} {id[^SuffixLength..].ToUpperInvariant()}"
            : deviceInfo.Name;
        return name.Length > Length.DeviceName ? name[..Length.DeviceName] : name;
    }

    //--------------------------------------------------------------------------------
    // Status
    //--------------------------------------------------------------------------------

    // アプリの版と電池の状態を報告する (電池の残りがわからなければ省く)
    public ValueTask<ApiResult<NoContent>> ReportStatusAsync()
    {
        var level = deviceState.BatteryChargeLevel;
        return deviceApi.ReportStatusAsync(new DeviceHeartbeatRequest
        {
            AppVersion = AppVersion(),
            BatteryLevel = level is >= 0 and <= 1 ? Math.Round((decimal)level, 2) : null,
            IsCharging = deviceState.BatteryState is BatteryState.Charging or BatteryState.Full
        });
    }

    private string AppVersion() =>
        $"{appInfo.VersionString} ({appInfo.BuildString})";
}
