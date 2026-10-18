namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.WebUtilities;

// 管理画面のサインインとアカウントの画面の経路
public static class AccountPaths
{
    // 画面に戻して知らせる失敗の印 (クエリの名前と値)
    public const string ErrorQuery = "error";

    public const string RateLimitedError = "limit";

    public const string Account = "/account";

    public const string SignIn = "/account/sign-in";

    public const string SignInTwoFactor = "/account/sign-in-2fa";

    public const string SignOut = "/account/sign-out";

    public const string Password = "/account/password";

    public const string TwoFactor = "/account/two-factor";

    public const string AccessDenied = "/account/access-denied";

    // サインインのあとに戻る先は、この管理画面の中の経路だけにする (ほかのサイトに飛ばされないように)
    public static string LocalReturnPath(string? returnPath) =>
        !String.IsNullOrEmpty(returnPath) &&
        returnPath.StartsWith('/') &&
        !returnPath.StartsWith("//", StringComparison.Ordinal) &&
        !returnPath.StartsWith("/\\", StringComparison.Ordinal)
            ? returnPath
            : "/";

    // 流量の制限で断ったフォームの送信を戻す先。同じ画面に失敗の印を付けて戻す (戻る先などのクエリは残す)
    public static string RateLimitedPath(PathString path, QueryString query)
    {
        var values = QueryHelpers.ParseQuery(query.Value);
        values[ErrorQuery] = RateLimitedError;
        return path.Add(QueryString.Create(values));
    }
}
