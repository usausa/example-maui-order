namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using TableOrder.Server.Web.Application.Account;

// サインイン。多要素を使う利用者は認証アプリのコードの画面に進み、仮のパスワードの利用者はパスワードの画面に進む
// どこを間違えたか (メールアドレス、パスワード、止めた利用者) は分けて出さない
public sealed partial class SignInPage
{
    private string? message;

    [SupplyParameterFromForm]
    private SignInInput? Input { get; set; }

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

    protected override void OnInitialized() => Input ??= new SignInInput();

    //--------------------------------------------------------------------------------
    // Sign in
    //--------------------------------------------------------------------------------

    private async Task SignInAsync()
    {
        var input = Input!;
        var result = await SignInManager.PasswordSignInAsync(input.Email, input.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await SignInManager.UserManager.FindByNameAsync(input.Email);
            if (user is not null)
            {
                await AccountService.RecordSignInAsync(user.Id, CancellationToken.None);
            }

            Navigation.NavigateTo(user?.MustChangePassword == true ? AccountPaths.Password : AccountPaths.LocalReturnPath(ReturnUrl));
            return;
        }

        if (result.RequiresTwoFactor)
        {
            Navigation.NavigateTo($"{AccountPaths.SignInTwoFactor}?returnUrl={Uri.EscapeDataString(AccountPaths.LocalReturnPath(ReturnUrl))}");
            return;
        }

        message = result.IsLockedOut
            ? "続けて間違えたため、しばらくサインインできません。15 分ほどしてから入れ直してください。"
            : "メールアドレスかパスワードが違います。";
    }
}
