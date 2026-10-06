namespace TableOrder.Contract.Visits;

// 確認のルール (Confirmation) にお客様が答えた記録 (年齢の確認など)
public sealed class VisitConfirmationRequest
{
    public Guid RuleId { get; set; }
}
