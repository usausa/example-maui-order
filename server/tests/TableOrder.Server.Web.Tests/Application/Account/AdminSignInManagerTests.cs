namespace TableOrder.Server.Web.Application.Account;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Models.Enums;

public sealed class AdminSignInManagerTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public AdminSignInManagerTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 資格の印が合っていても、止めた利用者と止めたテナントの利用者は Cookie の確かめ直しを通さない
    [Fact]
    public async Task SecurityStampValidationRefusesUsersWhoCannotSignIn()
    {
        // Arrange
        var active = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId);
        var inactive = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId, isActive: false);
        var (tenantId, _) = await factory.CreateTenantAsync();
        var suspended = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, tenantId);
        await factory.SuspendTenantAsync(tenantId);

        // Act / Assert
        Assert.True(await ValidateSecurityStampAsync(active));
        Assert.False(await ValidateSecurityStampAsync(inactive));
        Assert.False(await ValidateSecurityStampAsync(suspended));
    }

    private async ValueTask<bool> ValidateSecurityStampAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AdminUserEntity>>();
        var user = await signInManager.UserManager.FindByNameAsync(email);
        Assert.NotNull(user);
        return await signInManager.ValidateSecurityStampAsync(user, user.SecurityStamp);
    }
}
