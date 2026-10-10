namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

using TableOrder.Server.Web.Application.Account;

// 自分のアカウント (名前、役割、多要素) と、パスワード・多要素の画面とサインアウトへの入口
public sealed partial class AccountPage
{
    private AdminUserEntity? user;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required UserManager<AdminUserEntity> UserManager { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // 仮のパスワードの間は、パスワードを替えるまでほかの画面を開かない
    protected override async Task OnInitializedAsync()
    {
        user = await UserManager.GetUserAsync(HttpContext.User);
        if (user?.MustChangePassword == true)
        {
            Navigation.NavigateTo(AccountPaths.Password);
        }
    }
}
