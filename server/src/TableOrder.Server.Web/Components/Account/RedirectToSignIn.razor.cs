namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Account;

// サインインしていない (かサインインが切れた) 回線を、今の経路に戻れるようにしてサインインの画面に移す
public sealed partial class RedirectToSignIn
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override void OnInitialized()
    {
        var returnUrl = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
        Navigation.NavigateTo($"{AccountPaths.SignIn}?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
}
