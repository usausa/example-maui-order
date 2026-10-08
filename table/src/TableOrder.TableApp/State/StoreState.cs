namespace TableOrder.TableApp.State;

// 店舗の今の状態 (注文の一時停止、ラストオーダー、設定の版)。起動のときに読み、通知 (store.updated) で替える
public sealed class StoreState
{
    private string timeZone = string.Empty;

    private TimeOnly openTime;

    public bool OrderingPaused { get; private set; }

    public LocalizedText? PausedMessage { get; private set; }

    public TimeOnly? LastOrderTime { get; private set; }

    // チェーンと店舗の設定の版 (端末の設定の版と違えば、待受のときに起動からやり直して読み直す)
    public int SettingsVersion { get; private set; }

    public void Update(StoreResponse store)
    {
        timeZone = store.TimeZone;
        openTime = StoreHours.Parse(store.OpenTime);
        LastOrderTime = store.LastOrderTime is { } last ? StoreHours.Parse(last) : null;
        OrderingPaused = store.OrderingPaused;
        PausedMessage = store.PausedMessage;
        SettingsVersion = store.SettingsVersion;
    }

    // ラストオーダーまでの残り (ラストオーダーのない店は null、過ぎていれば負)
    public TimeSpan? UntilLastOrder(DateTimeOffset now) =>
        LastOrderTime is { } last ? StoreHours.UntilLastOrder(StoreHours.LocalTime(now, timeZone), openTime, last) : null;
}
