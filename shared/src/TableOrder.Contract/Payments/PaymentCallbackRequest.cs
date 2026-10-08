namespace TableOrder.Contract.Payments;

// 決済サービスからの支払の結果の通知 (仮の決済サービスの形。本物の決済サービスはそれぞれの形と署名で受ける)
public sealed class PaymentCallbackRequest
{
    public string ProviderReference { get; set; } = default!;

    // Completed か Failed
    public PaymentStatus Status { get; set; }

    public string? FailureReason { get; set; }
}
