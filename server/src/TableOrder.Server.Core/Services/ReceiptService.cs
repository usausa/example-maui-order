namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Bills;
using TableOrder.Server.Core.Accessors;

// 電子レシートの中身 (払い終えた来店の明細と税率ごとの額、払った方法と額)。日時は店舗のタイムゾーンで出す
// 事業者の名前と登録番号は、作ったときの店舗の設定 (登録番号がなければ、登録番号のないレシート)
public sealed record ReceiptDetailResult(
    LocalizedText BrandName,
    LocalizedText StoreName,
    string TimeZone,
    string TableName,
    string? IssuerName,
    string? InvoiceRegistrationNumber,
    DateTimeOffset IssuedAt,
    BillResponse Bill,
    IReadOnlyList<ReceiptPayment> Payments);

// 払い終えた支払
public sealed record ReceiptPayment(PaymentMethod Method, decimal Amount);

// 電子レシートの画面 (お客様が QR から開く。サインインはなく、推測できないトークンを知る人だけが開ける)
// テナントのわからない要求なので、トークンで引いた電子レシートのテナントと店舗で来店を読む
public sealed class ReceiptService
{
    private readonly DirectoryAccessor directoryAccessor;

    private readonly SettingsAccessor settingsAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly PaymentAccessor paymentAccessor;

    public ReceiptService(
        DirectoryAccessor directoryAccessor,
        SettingsAccessor settingsAccessor,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        PaymentAccessor paymentAccessor)
    {
        this.directoryAccessor = directoryAccessor;
        this.settingsAccessor = settingsAccessor;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.paymentAccessor = paymentAccessor;
    }

    // トークンの電子レシート。ないとき (片付けで来店と一緒に消えたときも) は null
    // 閉じた来店の明細は変わらないので、明細は写さずに会計と同じ計算で作る
    public async ValueTask<ReceiptDetailResult?> GetAsync(string token, CancellationToken cancellationToken)
    {
        var receipt = await directoryAccessor.QueryReceiptByTokenAsync(token, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var tenantId = receipt.TenantId;
        var storeId = receipt.StoreId;
        var visit = await visitAccessor.QueryAsync(tenantId, storeId, receipt.VisitId, cancellationToken);
        if (visit is null)
        {
            return null;
        }

        var brand = await settingsAccessor.QueryBrandAsync(tenantId, cancellationToken);
        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        var lines = await orderAccessor.QueryLineListAsync(tenantId, visit.Id, cancellationToken);
        var options = await orderAccessor.QueryLineOptionListAsync(tenantId, visit.Id, cancellationToken);
        var payments = await paymentAccessor.QueryListAsync(tenantId, visit.Id, cancellationToken);
        return new ReceiptDetailResult(
            brand!.BrandName,
            store!.Name,
            store.TimeZone,
            visit.TableName,
            receipt.IssuerName,
            receipt.InvoiceRegistrationNumber,
            receipt.IssuedAt,
            BillCalculator.Calculate(visit, store, lines, options, payments),
            payments.Where(static x => x.Status == PaymentStatus.Completed).Select(static x => new ReceiptPayment(x.Method, x.Amount)).ToList());
    }
}
