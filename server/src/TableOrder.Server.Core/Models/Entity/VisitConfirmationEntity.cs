namespace TableOrder.Server.Core.Models.Entity;

// 確認のルールに答えた記録
public sealed class VisitConfirmationEntity
{
    public Guid TenantId { get; set; }

    public Guid VisitId { get; set; }

    public Guid RuleId { get; set; }

    public Guid? DeviceId { get; set; }

    public DateTimeOffset ConfirmedAt { get; set; }
}
