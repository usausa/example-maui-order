namespace TableOrder.Server.Core.Infrastructure.Payments;

using TableOrder.Server.Core.Services;

// 仮の決済サービス (本物の決済サービスにつなぐまで使う)。結果は開発の環境の通知か、時間の経過で受ける
// QR の内容は本物と同じく、取引番号と額を入れた URL にする
public sealed class FakePaymentProvider : IPaymentProvider
{
    // QR の期限
    private static readonly TimeSpan QrLifetime = TimeSpan.FromMinutes(5);

    public string Name => "fake";

    public PaymentStart Start(Guid paymentId, PaymentMethod method, decimal amount, DateTimeOffset now)
    {
        var reference = paymentId.ToString("N");
        return method == PaymentMethod.QrCode
            ? new PaymentStart(reference, String.Create(CultureInfo.InvariantCulture, $"https://pay.example.jp/fake/{reference}?amount={amount}"), now + QrLifetime)
            : new PaymentStart(reference, null, null);
    }
}
