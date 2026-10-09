namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using TableOrder.Server.Web.Application.Account;

// 多要素を使う利用者のサインインの 2 つ目 (認証アプリのコードか、回復用のコード)
// パスワードを確かめたことは一時的な Cookie で受け取り、なければ (期限切れ) サインインからやり直す
public sealed partial class SignInTwoFactorPage
{
    private AdminUserEntity? user;

    private string? message;

    [SupplyParameterFromForm(FormName = "code")]
    private CodeInput? Code { get; set; }

    [SupplyParameterFromForm(FormName = "recovery")]
    private CodeInput? Recovery { get; set; }

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required SignInManager<AdminUserEntity> SignInManager { get; set; }

    [Inject]
    public required AccountService AccountService { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        Code ??= new CodeInput();
        Recovery ??= new CodeInput();
        user = await SignInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            Navigation.NavigateTo(AccountPaths.SignIn);
        }
    }

    //--------------------------------------------------------------------------------
    // Sign in
    //--------------------------------------------------------------------------------

    // 認証アプリは数字を区切って出すことがあるので、空白とハイフンを除く
    private async Task VerifyAsync()
    {
        if (user is null)
        {
            return;
        }

        var code = Code!.Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        await CompleteAsync(user, await SignInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false), "コードが違います。");
    }

    private async Task RecoverAsync()
    {
        if (user is null)
        {
            return;
        }

        var code = Recovery!.Code.Replace(" ", string.Empty, StringComparison.Ordinal);
        await CompleteAsync(user, await SignInManager.TwoFactorRecoveryCodeSignInAsync(code), "回復用のコードが違うか、使い終えています。");
    }

    private async Task CompleteAsync(AdminUserEntity signedIn, SignInResult result, string failure)
    {
        if (!result.Succeeded)
        {
            message = result.IsLockedOut
                ? "続けて間違えたため、しばらくサインインできません。15 分ほどしてから入れ直してください。"
                : failure;
            return;
        }

        await AccountService.RecordSignInAsync(signedIn.Id, CancellationToken.None);
        Navigation.NavigateTo(signedIn.MustChangePassword ? AccountPaths.Password : AccountPaths.LocalReturnPath(ReturnUrl));
    }
}
