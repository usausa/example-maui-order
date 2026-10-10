namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;

// 開いている管理画面 (Blazor の回線) のサインインを 1 分ごとに確かめ直す
// 資格の印が替わった (止めた、役割や店舗を替えた、パスワードを替えた) か、サインインできなくなったら、回線をサインインの前に戻す
// 回線の中の操作は HTTP の要求にならず Cookie を延ばさないので、回線を始めてから Cookie の期限 (8 時間) を過ぎても戻す
public sealed class AdminAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly TimeProvider timeProvider;

    private readonly IServiceScopeFactory scopeFactory;

    private readonly IdentityOptions options;

    private readonly DateTimeOffset expiresAt;

    public AdminAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> options,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
        : base(loggerFactory)
    {
        this.timeProvider = timeProvider;
        this.scopeFactory = scopeFactory;
        this.options = options.Value;
        expiresAt = timeProvider.GetUtcNow() + cookieOptions.Get(IdentityConstants.ApplicationScheme).ExpireTimeSpan;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() >= expiresAt)
        {
            return false;
        }

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
