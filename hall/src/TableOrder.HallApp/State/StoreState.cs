namespace TableOrder.HallApp.State;

// 店舗の設定 (端末の設定で受けた店舗の名前、呼び出しの用件、機能、注文のルール、この端末) と、店舗の今の状態 (注文の一時停止)
// 起動のときに読み、店舗の今の状態は通知 (store.updated) で替える
public sealed class StoreState
{
    public DeviceConfigResponse Config { get; private set; } = default!;

    public StoreResponse Store { get; private set; } = default!;

    public LocalizedText StoreName => Config.StoreName;

    // 登録した端末の名前 (管理画面で付け替えられる)
    public string? DeviceName => Config.Device?.Name;

    public bool OrderingPaused => Store.OrderingPaused;

    // 呼び出しの用件の名前 (店舗の設定にない用件は null)
    public LocalizedText? FindCallReasonName(string code) =>
        Config.CallReasons.FirstOrDefault(x => x.Code == code)?.Name;

    public void Update(DeviceConfigResponse config, StoreResponse store)
    {
        Config = config;
        Store = store;
    }

    public void UpdateStore(StoreResponse store)
    {
        Store = store;
    }
}
