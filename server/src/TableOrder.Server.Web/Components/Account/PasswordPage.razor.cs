namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

// パスワードの変更。仮のパスワードの利用者は、替えるまでほかの画面を開かない (替えたら管理画面に進む)
// 替えると資格の印が替わるので、この Cookie を出し直す (ほかで開いている管理画面はサインインからやり直す)
public sealed partial class PasswordPage
{
    private AdminUserEntity? user;

    private bool mustChange;

    private bool changed;

    private List<string> errors = [];

    [SupplyParameterFromForm]
    private PasswordInput? Input { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required SignInManager<AdminUserEntity> SignInManager { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        Input ??= new PasswordInput();
        user = await SignInManager.UserManager.GetUserAsync(HttpContext.User);
        mustChange = user?.MustChangePassword ?? false;
    }

    //--------------------------------------------------------------------------------
    // Password
    //--------------------------------------------------------------------------------

    // 仮のパスワードの印は、新しいパスワードと一緒に書く (替えられなければ戻す)
    // 今と同じパスワードには替えない (仮のパスワードのままだと、出した管理者が知ったままになる)
    private async Task ChangeAsync()
    {
        if (user is null)
        {
            return;
        }

        if (Input!.NewPassword == Input.CurrentPassword)
        {
            errors = ["新しいパスワードは、今のパスワードと違うものにしてください。"];
            return;
        }

        var temporary = user.MustChangePassword;
        user.MustChangePassword = false;
        var result = await SignInManager.UserManager.ChangePasswordAsync(user, Input!.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            user.MustChangePassword = temporary;
            errors = result.Errors.Select(AdminNames.IdentityError).Distinct().ToList();
            return;
        }

        await SignInManager.RefreshSignInAsync(user);
        if (temporary)
        {
            Navigation.NavigateTo("/");
            return;
        }

        changed = true;
    }
}
