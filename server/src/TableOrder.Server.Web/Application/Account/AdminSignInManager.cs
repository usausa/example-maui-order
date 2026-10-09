namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

// 管理画面のサインイン。止めた利用者と、止めたテナントの利用者はサインインさせない
// サインインのほか、Cookie の確かめ直し (資格の印) と出し直し (パスワードの変更など) でも確かめる (止める前に出た Cookie を使い続けさせない)
public sealed class AdminSignInManager : SignInManager<AdminUserEntity>
{
    private readonly AccountService accountService;

    public AdminSignInManager(
        UserManager<AdminUserEntity> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<AdminUserEntity> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<AdminUserEntity>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<AdminUserEntity> confirmation,
        AccountService accountService)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        this.accountService = accountService;
    }

    public override async Task<bool> CanSignInAsync(AdminUserEntity user) =>
        await base.CanSignInAsync(user) && await accountService.CanSignInAsync(user, CancellationToken.None);

    public override async Task<bool> ValidateSecurityStampAsync(AdminUserEntity? user, string? securityStamp) =>
        (user is not null) && await base.ValidateSecurityStampAsync(user, securityStamp) && await CanSignInAsync(user);

    // サインインできなくなった利用者には Cookie を出し直さず、サインアウトさせる
    public override async Task RefreshSignInAsync(AdminUserEntity user)
    {
        if (!await CanSignInAsync(user))
        {
            await SignOutAsync();
            return;
        }

        await base.RefreshSignInAsync(user);
    }
}
