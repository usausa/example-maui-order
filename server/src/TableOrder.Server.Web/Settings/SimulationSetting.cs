namespace TableOrder.Server.Web.Settings;

// 開発の環境で、スタッフと決済サービスの代わりに時間で進める (端末だけで流れを確かめるため)
public sealed class SimulationSetting
{
    public bool Enabled { get; set; }

    // 進めるものを見る間隔
    [Range(1, 60)]
    public int IntervalSeconds { get; set; } = 1;

    // 注文 (食後の品はお願い) から、作り始め・できあがり・提供まで
    [Range(0, 3600)]
    public int CookingSeconds { get; set; }

    [Range(0, 3600)]
    public int ReadySeconds { get; set; }

    [Range(0, 3600)]
    public int ServedSeconds { get; set; }

    // 呼び出しから向かうまでと、向かってから対応を終えるまで
    [Range(0, 3600)]
    public int AcknowledgeSeconds { get; set; }

    [Range(0, 3600)]
    public int CallDoneSeconds { get; set; }

    // 支払を始めてから払い終えるまで
    [Range(0, 3600)]
    public int PaymentSeconds { get; set; }
}
