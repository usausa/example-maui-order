namespace TableOrder.Contract.Bills;

// 領収の内容と電子レシートの URL (画面に QR で出す)
public sealed class ReceiptResponse
{
    public Uri Url { get; set; } = default!;

    public decimal Total { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
}
