namespace TableOrder.Server.Web.Settings;

public sealed class TokenSetting
{
    // トークンを出すサーバの名前 (アクセストークンの iss、端末のトークンの要求の aud)
    [Required]
    public string Issuer { get; set; } = default!;

    // この API (アクセストークンの aud)
    [Required]
    public string Audience { get; set; } = default!;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; }

    // 端末のトークンの要求の期限の上限
    [Range(1, 60)]
    public int AssertionMaxMinutes { get; set; }

    // 署名の鍵 (P-256 の秘密鍵の PEM)。開発の環境で空なら起動のたびに作る
    public string? SigningKey { get; set; }
}
