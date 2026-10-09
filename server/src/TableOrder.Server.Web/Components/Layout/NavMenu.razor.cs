namespace TableOrder.Server.Web.Components.Layout;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 管理画面のメニュー。役割で開けない画面は出さない (開いても画面のポリシーで断る)
public sealed partial class NavMenu
{
    [Inject]
    public required AdminScope Scope { get; set; }
}
