namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

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
    public required UserManager<AdminUserEntity> UserManager { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        user = await UserManager.GetUserAsync(HttpContext.User);
    }
}
