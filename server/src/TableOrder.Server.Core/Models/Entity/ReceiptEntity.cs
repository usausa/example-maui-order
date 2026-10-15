namespace TableOrder.Server.Core.Models.Entity;

// 電子レシート (支払が揃って来店を閉じたときに作る)
public sealed class ReceiptEntity
{
    public Guid TenantId { get; set; }

    public Guid VisitId { get; set; }

    public Guid StoreId { get; set; }

    // 電子レシートの画面で引く値 (推測できない長さにする)
    public string Token { get; set; } = default!;

    // 作ったときの店舗の事業者 (あとで店舗の設定を替えても、出したレシートは替えない)
    public string? IssuerName { get; set; }

    public string? InvoiceRegistrationNumber { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
}
