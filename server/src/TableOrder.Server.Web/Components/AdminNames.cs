namespace TableOrder.Server.Web.Components;

using Microsoft.AspNetCore.Identity;

// 管理画面に出す名前と文言
public static class AdminNames
{
    public static string RoleName(AdminRole role) =>
        role switch
        {
            AdminRole.Operator => "運営者",
            AdminRole.TenantAdmin => "テナントの管理者",
            _ => "店舗の担当"
        };

    public static string TenantStatusName(TenantStatus status) =>
        status switch
        {
            TenantStatus.Active => "使える",
            TenantStatus.Suspended => "止めた",
            _ => "解約した"
        };

    public static string TaxRoundingName(TaxRounding rounding) =>
        rounding switch
        {
            TaxRounding.Floor => "切り捨て",
            TaxRounding.Round => "四捨五入",
            _ => "切り上げ"
        };

    // 業務の処理の失敗を画面の文言にする (入力の誤りは項目の理由をそのまま出す)。画面ごとの文言 (specific が null 以外を返す errorCode) を先に使う
    public static string ErrorMessage(ServiceError error, Func<string, string?>? specific = null) =>
        error.Errors?.Values.SelectMany(static x => x).FirstOrDefault() ??
        specific?.Invoke(error.ErrorCode) ??
        error.ErrorCode switch
        {
            ErrorCodes.NotFound => "見つかりません。読み直してください",
            ErrorCodes.VersionMismatch => "ほかで替えられています。読み直してください",
            _ => error.ErrorCode
        };

    // 利用者 (運営者も) の状態
    public static string UserStatusName(AdminUserSummaryEntity user, DateTimeOffset now) =>
        !user.IsActive ? "止めた" :
        user.LockoutEnd > now ? "間違えたため止めている" :
        user.MustChangePassword ? "仮のパスワード" :
        "使える";

    // ASP.NET Core Identity の失敗 (英語) を画面の文言にする
    public static string IdentityError(IdentityError error) =>
        error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordMismatch) => "今のパスワードが違います。",
            nameof(IdentityErrorDescriber.PasswordTooShort) => "新しいパスワードは 12 文字以上にしてください。",
            nameof(IdentityErrorDescriber.ConcurrencyFailure) => "ほかの画面で替えられました。読み込み直してから、もう一度行ってください。",
            _ => "パスワードを替えられませんでした。"
        };
}
