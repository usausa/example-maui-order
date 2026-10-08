namespace TableOrder.Server.Web.Endpoints;

// API の失敗の応答 (Problem Details に errorCode を付ける)
public static class ApiProblems
{
    private const string ErrorCodeKey = "errorCode";

    private const string ErrorsKey = "errors";

    // errorCode ごとの HTTP の状態と文言 (端末は文言ではなく errorCode で扱いを決める)
    private static readonly Dictionary<string, (int Status, string Title)> Definitions = new(StringComparer.Ordinal)
    {
        [ErrorCodes.DeviceScope] = (StatusCodes.Status403Forbidden, "この端末では使えません"),
        [ErrorCodes.DeviceRevoked] = (StatusCodes.Status403Forbidden, "この端末は無効にされています"),
        [ErrorCodes.TenantSuspended] = (StatusCodes.Status403Forbidden, "ご利用を停止しています"),
        [ErrorCodes.NotFound] = (StatusCodes.Status404NotFound, "対象が見つかりません"),
        [ErrorCodes.DuplicateIdMismatch] = (StatusCodes.Status409Conflict, "同じ Id で内容の違う要求です"),
        [ErrorCodes.VersionMismatch] = (StatusCodes.Status409Conflict, "ほかの端末で変わっています。読み直してください"),
        [ErrorCodes.TableOccupied] = (StatusCodes.Status409Conflict, "テーブルには来店があります"),
        [ErrorCodes.EventsExpired] = (StatusCodes.Status410Gone, "通知を追いかけられません。今の状態を読み直してください"),
        [ErrorCodes.PairingCodeInvalid] = (StatusCodes.Status422UnprocessableEntity, "コードが正しくないか、期限が切れています"),
        [ErrorCodes.VisitNotOpen] = (StatusCodes.Status422UnprocessableEntity, "来店は終わっています"),
        [ErrorCodes.CheckoutInProgress] = (StatusCodes.Status422UnprocessableEntity, "会計中です"),
        [ErrorCodes.OrderingPaused] = (StatusCodes.Status422UnprocessableEntity, "注文を一時停止しています"),
        [ErrorCodes.LastOrderPassed] = (StatusCodes.Status422UnprocessableEntity, "ラストオーダーを過ぎました"),
        [ErrorCodes.MenuChanged] = (StatusCodes.Status422UnprocessableEntity, "メニューが変わりました"),
        [ErrorCodes.ItemSoldOut] = (StatusCodes.Status422UnprocessableEntity, "売り切れの商品があります"),
        [ErrorCodes.StockInsufficient] = (StatusCodes.Status422UnprocessableEntity, "残りの数を超える商品があります"),
        [ErrorCodes.OptionInvalid] = (StatusCodes.Status422UnprocessableEntity, "オプションの選び方を確かめてください"),
        [ErrorCodes.QuantityExceeded] = (StatusCodes.Status422UnprocessableEntity, "数量か明細の数が上限を超えています"),
        [ErrorCodes.ConfirmationRequired] = (StatusCodes.Status422UnprocessableEntity, "確認が必要な商品があります"),
        [ErrorCodes.LimitExceeded] = (StatusCodes.Status422UnprocessableEntity, "数の上限を超える商品があります"),
        [ErrorCodes.LineStatusInvalid] = (StatusCodes.Status422UnprocessableEntity, "明細の状態に合わない操作です"),
        [ErrorCodes.VisitHasOrders] = (StatusCodes.Status422UnprocessableEntity, "注文のある来店は取りやめられません"),
        [ErrorCodes.BillChanged] = (StatusCodes.Status422UnprocessableEntity, "会計の明細が変わりました"),
        [ErrorCodes.PaymentAmountInvalid] = (StatusCodes.Status422UnprocessableEntity, "支払の額を確かめてください"),
        [ErrorCodes.PaymentMethodUnavailable] = (StatusCodes.Status422UnprocessableEntity, "この支払方法は使えません")
    };

    public static ValidationProblem Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: "入力を確かめてください", extensions: Extensions(ErrorCodes.ValidationError));

    // ほかのテナントや店舗のものも同じにする (あるかどうかを見せない)
    public static ProblemHttpResult NotFound() => Problem(ErrorCodes.NotFound);

    public static ProblemHttpResult DeviceScope() => Problem(ErrorCodes.DeviceScope);

    public static ProblemHttpResult DeviceRevoked() => Problem(ErrorCodes.DeviceRevoked);

    public static ProblemHttpResult TenantSuspended() => Problem(ErrorCodes.TenantSuspended);

    public static ProblemHttpResult PairingCodeInvalid() => Problem(ErrorCodes.PairingCodeInvalid);

    // 業務の処理の失敗。明細ごとの理由 (品切れなど) は errors に明細の Id で入れる
    public static IResult From(ServiceError error) =>
        error.ErrorCode == ErrorCodes.ValidationError
            ? Validation(error.Errors?.ToDictionary(static x => x.Key, static x => x.Value) ?? [])
            : Problem(error.ErrorCode, error.Errors);

    // 定義していない errorCode は作りの誤りなので、例外にして 500 にする
    private static ProblemHttpResult Problem(string errorCode, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var (status, title) = Definitions.TryGetValue(errorCode, out var definition)
            ? definition
            : throw new InvalidOperationException($"Undefined error code. errorCode=[{errorCode}]");
        var extensions = Extensions(errorCode);
        if (errors is not null)
        {
            extensions[ErrorsKey] = errors;
        }

        return TypedResults.Problem(statusCode: status, title: title, extensions: extensions);
    }

    private static Dictionary<string, object?> Extensions(string errorCode) =>
        new() { [ErrorCodeKey] = errorCode };
}
