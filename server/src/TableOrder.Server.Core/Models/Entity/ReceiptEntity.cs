namespace TableOrder.Server.Core.Models.Entity;

// 電子レシート (支払が揃って来店を閉じたときに作る)
public sealed class ReceiptEntity
{
    public Guid TenantId { get; set; }

    public Guid VisitId { get; set; }

    // 電子レシートの画面で引く値 (推測できない長さにする)
    public string Token { get; set; } = default!;

    public DateTimeOffset IssuedAt { get; set; }
}
