namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Identity;

using TableOrder.Server.Web.Application.Authentication;

// サインインのクレーム (役割、テナント、受け持つ店舗、名前)。管理画面はこのクレームで扱える範囲を決める
// 役割や受け持つ店舗を替えたら資格の印を替えるので、古いクレームのままの管理画面は 1 分のうちにやり直す
public sealed class AdminClaimsPrincipalFactory : UserClaimsPrincipalFactory<AdminUserEntity>
{
    private readonly AccountService accountService;

    public AdminClaimsPrincipalFactory(
        UserManager<AdminUserEntity> userManager,
        IOptions<IdentityOptions> optionsAccessor,
        AccountService accountService)
        : base(userManager, optionsAccessor)
    {
        this.accountService = accountService;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AdminUserEntity user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AdminClaimNames.Role, user.Role.ToString()));
        identity.AddClaim(new Claim(AdminClaimNames.DisplayName, user.Name));
        if (user.TenantId is { } tenantId)
        {
            identity.AddClaim(new Claim(ClaimNames.TenantId, tenantId.ToString()));
        }

        foreach (var storeId in await accountService.GetStoreIdsAsync(user, CancellationToken.None))
        {
            identity.AddClaim(new Claim(AdminClaimNames.StoreId, storeId.ToString()));
        }

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(AdminClaimNames.MustChangePassword, bool.TrueString));
        }

        return identity;
    }
}
