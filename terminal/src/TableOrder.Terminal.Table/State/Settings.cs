namespace TableOrder.Terminal.Table.State;

using TableOrder.Terminal.Table.Components;

// 端末の設定 (IPreferences。キーはプロパティ名)。API の実装 (REST / モック) は IDeviceContext として読む
// 接続先と PIN は、EMM が管理対象の構成で配っていればそれを使う (端末の値は残し、配られなくなったら戻る)
// 端末の登録は接続先ごとなので、登録した接続先と組で持ち、接続先が替わったら登録していないものとする
#pragma warning disable CA1724
public sealed class Settings : IDeviceContext
{
    private const string DefaultStaffPin = "1234";

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

    // 注文サーバの URL。空ならモックの応答で動く
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

    // 登録で受け取った端末の id (今の接続先で登録していなければ null)
    public Guid? DeviceId =>
        (RegisteredEndPoint == ApiEndPoint) && Guid.TryParse(preferences.Get(nameof(DeviceId), string.Empty), out var id) ? id : null;

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

    // 登録と鍵を消す (次の登録で鍵を作り直す)
    public void Unregister()
    {
        preferences.Remove(nameof(DeviceId));
        preferences.Remove(nameof(RegisteredEndPoint));
        Key.Delete();
    }

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // スタッフメニューに入る PIN。EMM が配っていなければ端末ごとに持ち、初めは 1234 にする
    public string StaffPin
    {
        get => managed.StaffPin ?? preferences.Get(nameof(StaffPin), DefaultStaffPin);
        set => preferences.Set(nameof(StaffPin), value);
    }

    // PIN を EMM が配っている (端末では変えられない)
    public bool IsStaffPinManaged => managed.StaffPin is not null;
}
#pragma warning restore CA1724
