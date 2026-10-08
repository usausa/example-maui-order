namespace TableOrder.Server.Web.Settings;

public sealed class EventSetting
{
    // すべての店舗の通し番号を見る間隔 (ほかのサーバが書いた通知と、知らせを受け損ねた通知を送る)
    [Range(1, 60)]
    public int SweepSeconds { get; set; }

    // 残す時間を過ぎた通知を消す間隔
    [Range(1, 1440)]
    public int CleanupMinutes { get; set; }
}
