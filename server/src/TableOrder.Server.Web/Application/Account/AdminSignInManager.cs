namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

// 管理画面のサインイン。止めた利用者と、止めたテナントの利用者はサインインさせない
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
}
