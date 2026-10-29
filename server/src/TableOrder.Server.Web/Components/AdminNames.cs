namespace TableOrder.Server.Web.Components;

using Microsoft.AspNetCore.Identity;

// 管理画面に出す名前と文言
public static class AdminNames
{
    // 時刻のタイムゾーンをそろえられない (店舗がない、店舗によって違う) ときに使うもの
    public const string Utc = "UTC";

    // サインインと多要素のコードの送信を、流量の制限で断ったとき
    public const string RateLimitedMessage = "送信が続いたため、1 分ほど受け付けません";

    public static string RoleName(AdminRole role) =>
        role switch
        {
            AdminRole.Operator => "運営者",
            AdminRole.TenantAdmin => "テナント管理者",
            _ => "店舗担当者"
        };

    public static string TenantStatusName(TenantStatus status) =>
        status switch
        {
            TenantStatus.Active => "利用可能",
            TenantStatus.Suspended => "停止中",
            _ => "解約済み"
        };

    public static string TaxRoundingName(TaxRounding rounding) =>
        rounding switch
        {
            TaxRounding.Floor => "切り捨て",
            TaxRounding.Round => "四捨五入",
            _ => "切り上げ"
        };

    public static string DeviceKindName(DeviceKind kind) =>
        kind switch
        {
            DeviceKind.Table => "テーブル端末",
            DeviceKind.Hall => "ホール端末",
            DeviceKind.Kitchen => "キッチン端末",
            _ => "受付機"
        };

    public static string VisitStatusName(VisitStatus status) =>
        status switch
        {
            VisitStatus.Open => "来店中",
            VisitStatus.Paying => "会計中",
            VisitStatus.Closed => "終了",
            _ => "取消"
        };

    public static string OrderSourceName(OrderSource source) =>
        source == OrderSource.Hall ? "ホール端末" : "テーブル端末";

    public static string OrderLineStatusName(OrderLineStatus status) =>
        status switch
        {
            OrderLineStatus.Held => "食後に",
            OrderLineStatus.Ordered => "注文済み",
            OrderLineStatus.Cooking => "調理中",
            OrderLineStatus.Ready => "できあがり",
            OrderLineStatus.Served => "提供済み",
            _ => "取消"
        };

    public static string CallStatusName(CallStatus status) =>
        status switch
        {
            CallStatus.Open => "呼び出し中",
            CallStatus.Acknowledged => "対応中",
            _ => "対応済み"
        };

    // 業務の処理の失敗を画面の文言にする (入力の誤りは項目の理由をそのまま出す)。画面ごとの文言 (specific が null 以外を返す errorCode) を先に使う
    public static string ErrorMessage(ServiceError error, Func<string, string?>? specific = null) =>
        error.Errors?.Values.SelectMany(static x => x).FirstOrDefault() ??
        specific?.Invoke(error.ErrorCode) ??
        error.ErrorCode switch
        {
            ErrorCodes.NotFound => "対象が見つからないため、再読み込みしてください",
            ErrorCodes.VersionMismatch => "ほかで変更されたため、再読み込みしてください",
            _ => error.ErrorCode
        };

    // 店舗は名前とコードで出す (使わなくした店舗は、そうとわかるように)
    public static string StoreName(StoreEntity store) =>
        store.IsActive ? $"{store.Name.Ja} ({store.Code})" : $"{store.Name.Ja} ({store.Code}・利用停止中)";

    // 時刻は 1 つに決めたタイムゾーン (店舗の、そろえられなければ UTC) で出し、タイムゾーンは見出しに書く
    // 今日の動き (店内状況) は時刻だけ、近い日時 (期限、出した日時) は月日から、何年も前のことがある日時 (最後のサインイン、最後の通信) は年から出す
    public static string FormatTime(DateTimeOffset value, string timeZone) =>
        LocalDateTime(value, timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTimeOffset value, string timeZone) =>
        LocalDateTime(value, timeZone).ToString("M/d HH:mm", CultureInfo.InvariantCulture);

    public static string FormatFullDateTime(DateTimeOffset value, string timeZone) =>
        LocalDateTime(value, timeZone).ToString("yyyy/M/d HH:mm", CultureInfo.InvariantCulture);

    // 金額は 3 桁で区切って「円」を付ける (文の中に出すので、表の見出しに任せない)
    public static string FormatAmount(decimal amount) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} 円";

    // 件数、人数、台数は 3 桁で区切る
    public static string FormatCount(int count) =>
        count.ToString("N0", CultureInfo.InvariantCulture);

    private static DateTime LocalDateTime(DateTimeOffset value, string timeZone) =>
        timeZone == Utc ? value.UtcDateTime : StoreHours.LocalDateTime(value, timeZone);

    // 利用者 (運営者も) の状態
    public static string UserStatusName(AdminUserSummaryEntity user, DateTimeOffset now) =>
        !user.IsActive ? "停止中" :
        user.LockoutEnd > now ? "ロック中" :
        user.MustChangePassword ? "仮パスワード" :
        "利用可能";

    // ASP.NET Core Identity の失敗 (英語) を画面の文言にする
    public static string IdentityError(IdentityError error) =>
        error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordMismatch) => "現在のパスワードが正しくありません",
            nameof(IdentityErrorDescriber.PasswordTooShort) => "新しいパスワードは 12 文字以上にしてください",
            nameof(IdentityErrorDescriber.ConcurrencyFailure) => "ほかの画面で変更されたため、再読み込みしてからやり直してください",
            _ => "パスワードを変更できませんでした"
        };
}
