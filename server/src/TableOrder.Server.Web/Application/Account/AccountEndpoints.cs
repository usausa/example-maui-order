namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

// 管理画面のサインアウト (画面の帯のフォームから POST で受ける。ほかのサイトからの要求を断るため、フォームの印を確かめる)
public static class AccountEndpoints
{
    public static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost(AccountPaths.SignOut, static async Task<Results<BadRequest, RedirectHttpResult>> (HttpContext context, SignInManager<AdminUserEntity> signInManager) =>
            {
                // 本文を読まない経路は印の不正で止まらないので、確かめた結果 (UseAntiforgery が入れる) を見る
                if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
                {
                    return TypedResults.BadRequest();
                }

                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect("~" + AccountPaths.SignIn);
            })
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .ExcludeFromDescription();

        return app;
    }
}
