namespace TableOrder.Terminal.Table.State;

using TableOrder.Terminal.Table.Components;

// 端末の設定 (IPreferences。キーはプロパティ名)。API の実装は IDeviceContext として読む
// 接続先は、EMM が管理対象の構成で配っていればそれを使う (端末の値は残し、配られなくなったら戻る)
// 端末の登録は接続先ごとなので、登録した接続先と組で持ち、接続先が替わったら登録していないものとする
#pragma warning disable CA1724
public sealed class Settings : IDeviceContext
{
    private const string StaffPinIterationsKey = "StaffPinIterations";

    private const string StaffPinSaltKey = "StaffPinSalt";

    private const string StaffPinHashKey = "StaffPinHash";

    private readonly IPreferences preferences;

    private readonly ManagedConfiguration managed;

    public Settings(
        IPreferences preferences,
        ManagedConfiguration managed,
        DeviceKey key)
    {
        this.preferences = preferences;
        this.managed = managed;
        Key = key;
    }

    //--------------------------------------------------------------------------------
    // Server
    //--------------------------------------------------------------------------------

    // 注文サーバの URL。空なら起動で端末の設定に進む
    public string ApiEndPoint
    {
        get => managed.ApiEndPoint ?? preferences.Get(nameof(ApiEndPoint), string.Empty);
        set => preferences.Set(nameof(ApiEndPoint), value);
    }

    // 接続先を EMM が配っている (端末では変えられない)
    public bool IsApiEndPointManaged => managed.ApiEndPoint is not null;

    //--------------------------------------------------------------------------------
    // Registration
    //--------------------------------------------------------------------------------

    // 登録で受け取った端末の id (接続先がないか、今の接続先で登録していなければ null)
    public Guid? DeviceId =>
        !String.IsNullOrWhiteSpace(ApiEndPoint) && (RegisteredEndPoint == ApiEndPoint) && Guid.TryParse(preferences.Get(nameof(DeviceId), string.Empty), out var id) ? id : null;

    public bool IsRegistered => DeviceId is not null;

    // トークンの要求に署名する鍵
    public IDeviceKey Key { get; }

    // EMM が配る登録トークン (登録していなければ、起動したときにこれで登録する)
    public string? EnrollmentToken => managed.EnrollmentToken;

    // 登録した接続先
    private string? RegisteredEndPoint => preferences.Get<string?>(nameof(RegisteredEndPoint), null);

    // 今の接続先で登録した端末として覚える
    public void Register(Guid deviceId)
    {
        preferences.Set(nameof(DeviceId), deviceId.ToString("D"));
        preferences.Set(nameof(RegisteredEndPoint), ApiEndPoint);
    }

    // 登録と鍵と、登録と一緒に持つ PIN のハッシュを消す (次の登録で鍵を作り直す)
    public void Unregister()
    {
        preferences.Remove(nameof(DeviceId));
        preferences.Remove(nameof(RegisteredEndPoint));
        StaffPin = null;
        Key.Delete();
    }

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // スタッフメニューに入る PIN のハッシュ (店舗の設定で受け取り、登録と一緒に持つ。サーバにつながらないときも使う)
    // 受け取る前 (登録していない端末) は null
    public StaffPinHash? StaffPin
    {
        get => (preferences.Get(StaffPinIterationsKey, 0) is > 0 and var iterations) &&
               (preferences.Get<string?>(StaffPinSaltKey, null) is { } salt) &&
               (preferences.Get<string?>(StaffPinHashKey, null) is { } hash)
            ? new StaffPinHash(iterations, salt, hash)
            : null;
        set
        {
            if (value is null)
            {
                preferences.Remove(StaffPinIterationsKey);
                preferences.Remove(StaffPinSaltKey);
                preferences.Remove(StaffPinHashKey);
                return;
            }

            preferences.Set(StaffPinIterationsKey, value.Iterations);
            preferences.Set(StaffPinSaltKey, value.Salt);
            preferences.Set(StaffPinHashKey, value.Hash);
        }
    }
}
#pragma warning restore CA1724
