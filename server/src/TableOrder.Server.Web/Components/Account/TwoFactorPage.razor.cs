namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using TableOrder.Server.Web.Application.Account;

// 多要素 (認証アプリのワンタイムコード) の登録と外す、回復用のコードの出し直し
// 鍵は文字で出す (QR コードは出さない)。鍵を作り直すと資格の印が替わるので、この Cookie を出し直す
// 書けなかったとき (同じ利用者のほかの書き込みと重なった) は、Cookie を出し直さずに知らせ、読み直した利用者を出す (Cookie の印と DB を食い違わせない)
public sealed partial class TwoFactorPage
{
    // 認証アプリに出す発行者の名前
    private const string Issuer = "TableOrder";

    private const int RecoveryCodeCount = 10;

    private const string WriteFailed = "多要素の設定を書けませんでした。画面を開き直して、もう一度試してください。";

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
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required SignInManager<AdminUserEntity> SignInManager { get; set; }

    [Inject]
    public required IUserStore<AdminUserEntity> UserStore { get; set; }

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

        // 仮のパスワードの間は、パスワードを替えるまで多要素を登録させない
        if (user.MustChangePassword)
        {
            Navigation.NavigateTo(AccountPaths.Password);
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

        // 回復用のコードを書いてから使い始める (使い始めたのに回復用のコードがない、とならないように。コードだけを書いても使い始める前は効かない)
        var codes = await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        if ((codes is null) || !(await UserManager.SetTwoFactorEnabledAsync(user, true)).Succeeded)
        {
            await FailAsync(user);
            return;
        }

        recoveryCodes = codes.ToList();
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
            if (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount) is not { } codes)
            {
                await FailAsync(user);
                return;
            }

            recoveryCodes = codes.ToList();
            return;
        }

        if (Action.Action == "disable")
        {
            // 外したら鍵と回復用のコードも捨てる (もう一度使うときは登録し直す)
            // 外すことと捨てることは 1 回で書く (途中で書けずに、印だけが替わった Cookie にならないように)
            await ((IUserTwoFactorStore<AdminUserEntity>)UserStore).SetTwoFactorEnabledAsync(user, false, CancellationToken.None);
            await ((IUserTwoFactorRecoveryCodeStore<AdminUserEntity>)UserStore).ReplaceCodesAsync(user, [], CancellationToken.None);
            await ((IUserAuthenticatorKeyStore<AdminUserEntity>)UserStore).SetAuthenticatorKeyAsync(user, UserManager.GenerateNewAuthenticatorKey(), CancellationToken.None);
            if (!(await UserManager.UpdateSecurityStampAsync(user)).Succeeded)
            {
                await FailAsync(user);
                return;
            }

            await SignInManager.RefreshSignInAsync(user);
            enabled = false;
            await LoadKeyAsync(user);
        }
    }

    // 書けなかったら知らせ、書いた前の利用者を読み直して出す (メモリの利用者は書けなかった値に替わっている)
    private async Task FailAsync(AdminUserEntity target)
    {
        message = WriteFailed;
        user = await UserManager.FindByIdAsync(target.Id.ToString());
        enabled = user?.TwoFactorEnabled ?? false;
        recoveryCodes = [];
    }

    // まだ鍵がなければ作る
    private async Task LoadKeyAsync(AdminUserEntity target)
    {
        var key = await UserManager.GetAuthenticatorKeyAsync(target);
        if (String.IsNullOrEmpty(key))
        {
            if (!(await UserManager.ResetAuthenticatorKeyAsync(target)).Succeeded)
            {
                message = WriteFailed;
                return;
            }

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
