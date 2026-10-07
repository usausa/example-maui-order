namespace TableOrder.Server.Web.Settings;

public sealed class RateLimitSetting
{
    // 端末の登録の 1 分の回数 (接続元ごと)
    [Range(1, 100_000)]
    public int PairingPerMinute { get; set; }
}
