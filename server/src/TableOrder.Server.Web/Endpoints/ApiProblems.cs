namespace TableOrder.Server.Web.Endpoints;

// API の失敗の応答 (Problem Details に errorCode を付ける)
public static class ApiProblems
{
    private const string ErrorCodeKey = "errorCode";

    public static ValidationProblem Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: "入力を確かめてください", extensions: Extensions(ErrorCodes.ValidationError));

    // ほかのテナントや店舗のものも同じにする (あるかどうかを見せない)
    public static ProblemHttpResult NotFound() =>
        Problem(StatusCodes.Status404NotFound, ErrorCodes.NotFound, "対象が見つかりません");

    public static ProblemHttpResult DeviceScope() =>
        Problem(StatusCodes.Status403Forbidden, ErrorCodes.DeviceScope, "この端末では使えません");

    public static ProblemHttpResult DeviceRevoked() =>
        Problem(StatusCodes.Status403Forbidden, ErrorCodes.DeviceRevoked, "この端末は無効にされています");

    public static ProblemHttpResult TenantSuspended() =>
        Problem(StatusCodes.Status403Forbidden, ErrorCodes.TenantSuspended, "ご利用を停止しています");

    public static ProblemHttpResult PairingCodeInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, ErrorCodes.PairingCodeInvalid, "コードが正しくないか、期限が切れています");

    private static ProblemHttpResult Problem(int status, string errorCode, string title) =>
        TypedResults.Problem(statusCode: status, title: title, extensions: Extensions(errorCode));

    private static Dictionary<string, object?> Extensions(string errorCode) =>
        new() { [ErrorCodeKey] = errorCode };
}
