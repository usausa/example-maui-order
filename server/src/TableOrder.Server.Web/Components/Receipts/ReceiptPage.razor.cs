namespace TableOrder.Server.Web.Components.Receipts;

using Microsoft.AspNetCore.Components;

using TableOrder.Contract.Bills;

// 電子レシート (お客様が、テーブル端末のお礼の画面の QR から開く。サインインはなく、推測できないトークンを知る人だけが開ける)
// 言語はブラウザの言語 (日本語、英語) で選び、品の名前とオプションはメニューの名前のその言語 (ない言語は日本語) を出す
// 検索に載せず、ブラウザと途中の経路に残させない。見つからないトークンは 404 にし、あるかないかのほかは見せない
public sealed partial class ReceiptPage
{
    private ReceiptDetailResult? receipt;

    private bool english;

    // 税率が 2 つ以上あるときは、明細に税率を添える (どの品がどの税率かを示す)
    private bool multipleRates;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Parameter]
    public string Token { get; set; } = default!;

    [CascadingParameter]
    public HttpContext HttpContext { get; set; } = default!;

    [Inject]
    public required ReceiptService ReceiptService { get; set; }

    private ReceiptTexts Texts => english ? ReceiptTexts.English : ReceiptTexts.Japanese;

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // キャッシュの見出しは、静的な画面に付く偽造防止の値と同じにする (違うと、偽造防止が上書きして警告を出す)
    protected override async Task OnInitializedAsync()
    {
        var response = HttpContext.Response;
        response.Headers.CacheControl = "no-cache, no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Robots-Tag"] = "noindex";
        english = PrefersEnglish(HttpContext.Request);

        receipt = await ReceiptService.GetAsync(Token, CancellationToken.None);
        if (receipt is null)
        {
            // Blazor は画面が 404 にした応答の本文を書かない (StatusCodePages の作り直しに任せる) ので、本文を書き始めるときに 404 にする
            response.OnStarting(static state =>
            {
                ((HttpResponse)state).StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }, response);
            return;
        }

        multipleRates = receipt.Bill.Taxes.Count > 1;
    }

    // Accept-Language の重みの順に、日本語と英語の先に出たほう (どちらもなければ日本語)
    private static bool PrefersEnglish(HttpRequest request)
    {
        foreach (var language in request.GetTypedHeaders().AcceptLanguage.Where(static x => x.Quality is not 0).OrderByDescending(static x => x.Quality ?? 1))
        {
            var tag = language.Value.Value ?? String.Empty;
            if (IsLanguage(tag, "ja"))
            {
                return false;
            }

            if (IsLanguage(tag, "en"))
            {
                return true;
            }
        }

        return false;
    }

    // "en" と "en-US" のように、言語の部分で比べる
    private static bool IsLanguage(string tag, string language) =>
        tag.Equals(language, StringComparison.OrdinalIgnoreCase) || tag.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase);

    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    private string Text(LocalizedText text) =>
        english && !String.IsNullOrEmpty(text.En) ? text.En : text.Ja;

    private string LineName(BillResponseLine line) =>
        multipleRates ? $"{Text(line.Name)} ({Rate(line.TaxRate)})" : Text(line.Name);

    private string MethodName(PaymentMethod method) =>
        method == PaymentMethod.CreditCard ? Texts.CreditCard : Texts.QrCode;

    // 払い終えた日時は店舗のタイムゾーンで出す (お客様の端末のタイムゾーンによらない)
    private static string IssuedAt(ReceiptDetailResult value) =>
        StoreHours.LocalDateTime(value.IssuedAt, value.TimeZone).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    private static string Amount(decimal value) =>
        $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    private static string Rate(decimal rate) =>
        $"{(rate * 100).ToString("0.#", CultureInfo.InvariantCulture)}%";
}
