namespace TableOrder.KitchenApp.State;

// 端末の設定。接続先はアプリを配ったサーバにし、登録した端末の id は登録した接続先と組でブラウザに保存する
// 今の接続先と違えば登録していないものとする (接続先ごとに登録する)
public sealed class Settings : IDeviceContext
{
    private const string EndPointKey = "tableorder.registeredEndPoint";

    private const string DeviceIdKey = "tableorder.deviceId";

    private readonly BrowserStorage storage;

    public string ApiEndPoint { get; }

    public Guid? DeviceId =>
        (storage.Get(EndPointKey) == ApiEndPoint) && Guid.TryParse(storage.Get(DeviceIdKey), out var id) ? id : null;

    public IDeviceKey Key { get; }

    public bool IsRegistered => DeviceId is not null;

    public Settings(
        NavigationManager navigation,
        BrowserStorage storage,
        BrowserDeviceKey key)
    {
        this.storage = storage;
        Key = key;

        // アプリは /kitchen/ の下で配るので、API はサーバの起点に送る
        ApiEndPoint = new Uri(new Uri(navigation.BaseUri), "/").ToString();
    }

    public void Register(Guid deviceId)
    {
        storage.Set(EndPointKey, ApiEndPoint);
        storage.Set(DeviceIdKey, deviceId.ToString("D"));
    }

    // 無効にされた端末の登録と鍵を消す (登録からやり直す)
    public void Unregister()
    {
        storage.Remove(EndPointKey);
        storage.Remove(DeviceIdKey);
        Key.Delete();
    }
}
