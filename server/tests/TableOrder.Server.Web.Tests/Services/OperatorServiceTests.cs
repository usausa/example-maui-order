namespace TableOrder.Server.Web.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Services;

public sealed class OperatorServiceTests : IClassFixture<ServerFactory>
{
    private const string TemporaryPassword = "temporary-password-1";

    private static readonly string TemporaryHash = new PasswordHasher<AdminUserEntity>().HashPassword(new AdminUserEntity(), TemporaryPassword);

    private readonly ServerFactory factory;

    public OperatorServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private OperatorService Service => factory.Services.GetRequiredService<OperatorService>();

    // 足した運営者は一覧に出て、仮のパスワードでサインインするとパスワードの画面に進む。テナントの利用者は一覧に出ない
    [Fact]
    public async Task AddCreatesOperator()
    {
        // Arrange
        var email = $"operator-{Guid.NewGuid():N}@example.com";

        // Act
        ServiceResult<AdminUserSummaryEntity> result;
        List<AdminUserSummaryEntity> operators;
        using (factory.BeginTenant(null))
        {
            result = await Service.AddAsync(email, email.ToUpperInvariant(), "運営者", TemporaryHash, TestContext.Current.CancellationToken);
            operators = await Service.GetListAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(operators, x => x.Id == result.Value.Id);
        Assert.DoesNotContain(operators, x => x.Email == "admin@demo.example.com");
        using var admin = new TestAdmin(factory);
        using var signIn = await admin.SignInAsync(email, TemporaryPassword);
        Assert.Equal("/account/password", TestAdmin.LocationOf(signIn));
    }

    // 自分は止められず、ほかの運営者は止められる
    [Fact]
    public async Task OperatorCannotDeactivateSelf()
    {
        // Arrange
        using var scope = factory.BeginTenant(null);
        var self = (await Service.AddAsync($"self-{Guid.NewGuid():N}@example.com", $"SELF-{Guid.NewGuid():N}@EXAMPLE.COM", "自分", TemporaryHash, TestContext.Current.CancellationToken)).Value!;
        var other = (await Service.AddAsync($"other-{Guid.NewGuid():N}@example.com", $"OTHER-{Guid.NewGuid():N}@EXAMPLE.COM", "ほか", TemporaryHash, TestContext.Current.CancellationToken)).Value!;

        // Act
        var own = await Service.SetActiveAsync(self.Id, false, self.Version, self.Id, TestContext.Current.CancellationToken);
        var others = await Service.SetActiveAsync(other.Id, false, other.Version, self.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("isActive", own!.Errors!.Keys);
        Assert.Null(others);
        Assert.False(Assert.Single(await Service.GetListAsync(TestContext.Current.CancellationToken), x => x.Id == other.Id).IsActive);
    }

    // テナントの利用者は運営者として替えない (見つからないものとする)
    [Fact]
    public async Task TenantUserIsNotOperator()
    {
        // Arrange
        var email = await factory.CreateAdminUserAsync(Core.Models.Enums.AdminRole.TenantAdmin, SampleData.DemoTenantId);
        var user = (await factory.Services.GetRequiredService<AccountService>().FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!;
        using var scope = factory.BeginTenant(null);

        // Act
        var password = await Service.ResetPasswordAsync(user.Id, TemporaryHash, TestContext.Current.CancellationToken);
        var active = await Service.SetActiveAsync(user.Id, false, user.Version, Guid.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ErrorCodes.NotFound, password?.ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, active?.ErrorCode);
    }
}
