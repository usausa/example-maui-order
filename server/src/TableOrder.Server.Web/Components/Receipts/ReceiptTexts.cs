namespace TableOrder.Server.Web.Components.Receipts;

// 電子レシートの画面の文言 (ブラウザの言語で日本語か英語を選ぶ)。テーブルの名前と税率は文言の後と前に並べる
public sealed record ReceiptTexts(
    string Language,
    string Title,
    string Table,
    string Total,
    string Taxable,
    string IncludedTax,
    string Payment,
    string QrCode,
    string CreditCard,
    string RegistrationNumber,
    string NotFound,
    string NotFoundDetail)
{
    public static ReceiptTexts Japanese { get; } = new(
        "ja",
        "電子レシート",
        "テーブル",
        "合計 (税込)",
        "対象",
        "内消費税",
        "お支払い",
        "QR コード決済",
        "クレジットカード",
        "登録番号",
        "電子レシートが見つかりません",
        "表示期限を過ぎたか、URL が正しくありません");

    public static ReceiptTexts English { get; } = new(
        "en",
        "E-receipt",
        "Table",
        "Total (tax incl.)",
        "taxable",
        "Tax included",
        "Payment",
        "QR code payment",
        "Credit card",
        "Registration No.",
        "Receipt not found",
        "Please check the URL. Receipts past the viewing period are not available.");
}
