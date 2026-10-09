namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;

// 開いている管理画面 (Blazor の回線) のサインインを 1 分ごとに確かめ直す
// 資格の印が替わった (止めた、役割や店舗を替えた、パスワードを替えた) か、サインインできなくなったら、回線をサインインの前に戻す
public sealed class AdminAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory scopeFactory;

    private readonly IdentityOptions options;

    public AdminAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options)
        : base(loggerFactory)
    {
        this.scopeFactory = scopeFactory;
        this.options = options.Value;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUserEntity>>();
        var user = await userManager.GetUserAsync(authenticationState.User);
        if (user is null)
        {
            return false;
        }

        var stamp = authenticationState.User.FindFirstValue(options.ClaimsIdentity.SecurityStampClaimType);
        if (stamp != await userManager.GetSecurityStampAsync(user))
        {
            return false;
        }

        return await scope.ServiceProvider.GetRequiredService<AccountService>().CanSignInAsync(user, cancellationToken);
    }
}
