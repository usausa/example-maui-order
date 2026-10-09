namespace TableOrder.KitchenApp.State;

// 端末の設定 (店舗の名前、端末、受け持つ持ち場、機能の有無)、店舗の今の状態、メニュー (持ち場の名前と品切れにする品)
// 起動で読み、店舗は通知 (store.updated) で替える
public sealed class StoreState
{
    public DeviceConfigResponse Config { get; private set; } = default!;

    public StoreResponse Store { get; private set; } = default!;

    public MenuResponse Menu { get; private set; } = default!;

    // 受け持つ持ち場 (メニューの並びの順)
    public IReadOnlyList<MenuResponseStation> Stations { get; private set; } = [];

    // チケットを注意の色にするまでの時間 (0 は色を替えない)
    public TimeSpan AlertTime => TimeSpan.FromMinutes(Config.Features.KitchenAlertMinutes);

    // チェーンと店舗の設定の版が、起動で読んだ端末の設定と違う (起動からやり直して読み直す)
    public bool IsSettingsChanged => Store.SettingsVersion != Config.SettingsVersion;

    public void Update(DeviceConfigResponse config) => Config = config;

    public void Update(StoreResponse store) => Store = store;

    public void Update(MenuResponse menu)
    {
        Menu = menu;
        var assigned = Config.Device?.StationIds ?? [];
        Stations = menu.Stations.Where(x => assigned.Contains(x.Id)).OrderBy(static x => x.SortOrder).ToList();
    }
}
