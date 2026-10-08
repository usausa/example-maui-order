namespace TableOrder.Server.Core.Models.Entity;

// 支払 (決済の処理は決済サービスが行い、開始と結果を記録する)
public sealed class PaymentEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid VisitId { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; }

    // 店舗が見せる QR の内容 (QR コード決済)
    public string? QrCode { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string? Provider { get; set; }

    public string? ProviderReference { get; set; }

    public string? FailureReason { get; set; }

    public Guid? DeviceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
