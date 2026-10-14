namespace TableOrder.Server.Web.Infrastructure.Security;

public sealed class SecurityHeadersOption
{
    // Content-Security-Policy を強制せず、違反の報告だけにする (Report-Only のヘッダーで送る)
    public bool ReportOnly { get; set; }

    // Content-Security-Policy の値。"{nonce}" は要求ごとの CspNonce に置き換える。null なら送らない
    public string? ContentSecurityPolicy { get; set; }
}
