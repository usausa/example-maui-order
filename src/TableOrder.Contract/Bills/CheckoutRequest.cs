namespace TableOrder.Contract.Bills;

// 会計を始める (来店を Paying にして、注文を止める)
public sealed class CheckoutRequest
{
    // 表示していた明細 (変わっていれば受け付けない)
    public string BillVersion { get; set; } = default!;

    public int Version { get; set; }
}
