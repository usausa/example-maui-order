namespace TableOrder.Contract.Payments;

public sealed class PaymentResponse
{
    public Guid Id { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; }

    // 店舗が見せる QR の内容 (QrCode のとき)
    public string? QrCode { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
