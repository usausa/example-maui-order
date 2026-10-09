namespace TableOrder.Server.Web.Application.Account;

// 管理画面のサインインとアカウントの画面の経路
public static class AccountPaths
{
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
}
