namespace TableOrder.Server.Core.Services;

// 支払を始めたときに決済サービスから受け取る値 (取引番号、店舗が見せる QR の内容と期限)
public sealed record PaymentStart(string? Reference, string? QrCode, DateTimeOffset? ExpiresAt);

// 決済サービス。決済の処理そのものは決済サービスが行い、注文サーバは支払の開始と結果をつなぐ
public interface IPaymentProvider
{
    // 決済サービスの名前 (支払の Provider と、結果の通知の経路に使う)
    string Name { get; }

    // 支払を始める。クレジットカードはテーブルの決済端末で払うので、QR の内容を返さない
    PaymentStart Start(Guid paymentId, PaymentMethod method, decimal amount, DateTimeOffset now);
}
