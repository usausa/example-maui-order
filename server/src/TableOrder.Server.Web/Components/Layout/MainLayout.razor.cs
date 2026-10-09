namespace TableOrder.Server.Web.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

using TableOrder.Server.Web.Application.Account;
using TableOrder.Server.Web.Application.Context;

// 管理画面の入れ物。サインインのクレームから扱える範囲を作り終えてから画面を出す (画面は範囲で絞った店舗の文脈で Service を呼ぶ)
// 仮のパスワードでサインインした利用者は、パスワードを替えるまでほかの画面を開かない
public sealed partial class MainLayout
{
    private ErrorBoundary? errorBoundary;

    private bool drawerOpen = true;

    private bool initialized;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AdminScope Scope { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationState is not null)
        {
            Scope.Initialize((await AuthenticationState).User);
        }

        if (Scope.MustChangePassword)
        {
            Navigation.NavigateTo(AccountPaths.Password, forceLoad: true);
            return;
        }

        initialized = true;
    }

    protected override void OnParametersSet() => errorBoundary?.Recover();

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    private void ToggleDrawer() => drawerOpen = !drawerOpen;

    private void Recover() => errorBoundary?.Recover();
}
