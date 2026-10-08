namespace TableOrder.Contract.Stores;

// 店舗 (注文の一時停止とラストオーダーは、テーブル端末が表示と注文の受け付けに使う)
public sealed class StoreResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    // 現地時刻のタイムゾーン ("Asia/Tokyo")
    public string TimeZone { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }

    // 現地時刻の "HH:mm"。開店の時刻を営業日の区切りにする (日をまたぐ営業は閉店の時刻が開店より前になる)
    public string OpenTime { get; set; } = default!;

    public string CloseTime { get; set; } = default!;

    // 過ぎたら注文を受け付けない。ラストオーダーのない店は null
    public string? LastOrderTime { get; set; }

    // 注文の一時停止 (厨房が追いつかないときなど)
    public bool OrderingPaused { get; set; }

    // 一時停止の間にテーブル端末に出す文言
    public LocalizedText? PausedMessage { get; set; }

    public TaxRounding TaxRounding { get; set; }

    // チェーンと店舗の設定の版 (端末の設定の版と違えば、テーブル端末は待受のときに起動からやり直す)
    public int SettingsVersion { get; set; }
}
