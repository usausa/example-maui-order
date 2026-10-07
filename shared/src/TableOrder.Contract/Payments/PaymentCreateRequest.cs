namespace TableOrder.Contract.Payments;

// 支払を始める。Id は端末が採番する
public sealed class PaymentCreateRequest
{
    public Guid Id { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }
}
