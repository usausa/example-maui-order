namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

// 多要素 (認証アプリのワンタイムコード) の登録と外す、回復用のコードの出し直し
// 鍵は文字で出す (QR コードは出さない)。鍵を作り直すと資格の印が替わるので、この Cookie を出し直す
public sealed partial class TwoFactorPage
{
    // 認証アプリに出す発行者の名前
    private const string Issuer = "TableOrder";

    private const int RecoveryCodeCount = 10;

    private AdminUserEntity? user;

    private bool enabled;

    private string sharedKey = string.Empty;

    private string authenticatorUri = string.Empty;

    private List<string> recoveryCodes = [];

    private string? message;

    [SupplyParameterFromForm(FormName = "enable")]
    private CodeInput? Code { get; set; }

    [SupplyParameterFromForm(FormName = "enabled")]
    private ActionInput? Action { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required SignInManager<AdminUserEntity> SignInManager { get; set; }

    private UserManager<AdminUserEntity> UserManager => SignInManager.UserManager;

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        Code ??= new CodeInput();
        Action ??= new ActionInput();
        user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            return;
        }

        enabled = user.TwoFactorEnabled;
        if (!enabled)
        {
            await LoadKeyAsync(user);
        }
    }

    //--------------------------------------------------------------------------------
    // Two factor
    //--------------------------------------------------------------------------------

    // コードが合ったら使い始め、回復用のコードを出す
    private async Task EnableAsync()
    {
        if (user is null)
        {
            return;
        }

        var code = Code!.Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (!await UserManager.VerifyTwoFactorTokenAsync(user, UserManager.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            message = "コードが違います。認証アプリの時刻が合っているかも確かめてください。";
            return;
        }

        await UserManager.SetTwoFactorEnabledAsync(user, true);
        recoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? [];
        await SignInManager.RefreshSignInAsync(user);
        enabled = true;
    }

    private async Task HandleAsync()
    {
        if (user is null)
        {
            return;
        }

        if (Action!.Action == "codes")
        {
            recoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? [];
            return;
        }

        if (Action.Action == "disable")
        {
            // 外したら鍵と回復用のコードも捨てる (もう一度使うときは登録し直す)
            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
            await UserManager.ResetAuthenticatorKeyAsync(user);
            await SignInManager.RefreshSignInAsync(user);
            enabled = false;
            await LoadKeyAsync(user);
        }
    }

    // まだ鍵がなければ作る
    private async Task LoadKeyAsync(AdminUserEntity target)
    {
        var key = await UserManager.GetAuthenticatorKeyAsync(target);
        if (String.IsNullOrEmpty(key))
        {
            await UserManager.ResetAuthenticatorKeyAsync(target);
            await SignInManager.RefreshSignInAsync(target);
            key = await UserManager.GetAuthenticatorKeyAsync(target) ?? string.Empty;
        }

        sharedKey = FormatKey(key);
        authenticatorUri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(target.Email)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
    }

    // 手で入れやすいように、4 文字ごとに区切って出す
    private static string FormatKey(string key)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return builder.ToString();
    }
}
