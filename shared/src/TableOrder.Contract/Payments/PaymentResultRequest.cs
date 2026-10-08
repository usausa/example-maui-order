namespace TableOrder.Contract.Payments;

// テーブルの決済端末で払った結果 (クレジットカード)
public sealed class PaymentResultRequest
{
    // Completed か Failed
    public PaymentStatus Status { get; set; }

    public string? Provider { get; set; }

    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }
}
