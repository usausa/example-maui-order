namespace TableOrder.Server.Web.Settings;

// 古いデータの片付け (残す時間を過ぎた通知、期限を過ぎたペアリングコードと登録トークン、閉じた来店、公開し直したメニュー)
public sealed class CleanupSetting
{
    // 片付ける間隔
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;

    // 閉じた来店を残す日数 (来店の営業日から数える)
    [Range(1, 3650)]
    public int VisitRetentionDays { get; set; } = 90;

    // 閉じた来店を 1 つのトランザクションで消す数 (ほかの書き込みを長く止めない)
    [Range(1, 10000)]
    public int VisitBatchSize { get; set; } = 200;

    // 店舗ごとに残すメニューの公開の数 (今のメニューのほかに、新しい順)
    [Range(1, 1000)]
    public int MenuPublicationsKept { get; set; } = 10;
}
