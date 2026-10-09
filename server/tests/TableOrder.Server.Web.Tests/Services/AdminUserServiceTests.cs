namespace TableOrder.Server.Web.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Models.Enums;
using TableOrder.Server.Core.Services;

public sealed class AdminUserServiceTests : IClassFixture<ServerFactory>
{
    private const string TemporaryPassword = "temporary-password-1";

    private static readonly string TemporaryHash = new PasswordHasher<AdminUserEntity>().HashPassword(new AdminUserEntity(), TemporaryPassword);

    private readonly ServerFactory factory;

    private int lastUser;

    public AdminUserServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private AdminUserService Service => factory.Services.GetRequiredService<AdminUserService>();

    private AccountService Account => factory.Services.GetRequiredService<AccountService>();

    // 足した利用者は仮のパスワードでサインインし、パスワードの画面に進む
    [Fact]
    public async Task AddCreatesUserWithTemporaryPassword()
    {
        // Arrange
        var email = NextEmail();

        // Act
        ServiceResult<AdminUserSummaryEntity> result;
        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            result = await Service.AddAsync(Input(email, AdminRole.StoreStaff, SampleData.DemoStoreId), TemporaryHash, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Value.MustChangePassword);
        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            var item = Assert.Single(await Service.GetListAsync(TestContext.Current.CancellationToken), x => x.User.Id == result.Value.Id);
            Assert.Equal([SampleData.DemoStoreId], item.StoreIds);
        }

        using var admin = new TestAdmin(factory);
        using var signIn = await admin.SignInAsync(email, TemporaryPassword);
        Assert.Equal("/account/password", TestAdmin.LocationOf(signIn));
    }

    // メールアドレスはすべてのテナントで一意 (ほかのテナントの利用者とも重ねない)
    [Fact]
    public async Task AddRejectsEmailInUse()
    {
        // Arrange
        using var scope = factory.BeginTenant(SampleData.DemoTenantId);

        // Act
        var result = await Service.AddAsync(Input("admin@test.example.com", AdminRole.TenantAdmin), TemporaryHash, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ValidationError, result.Error.ErrorCode);
        Assert.Contains("email", result.Error.Errors!.Keys);
    }

    // 店舗の担当は、テナントの店舗を 1 つ以上受け持つ
    [Fact]
    public async Task StoreStaffRequiresTenantStores()
    {
        // Arrange
        using var scope = factory.BeginTenant(SampleData.DemoTenantId);

        // Act
        var none = await Service.AddAsync(Input(NextEmail(), AdminRole.StoreStaff), TemporaryHash, TestContext.Current.CancellationToken);
        var other = await Service.AddAsync(Input(NextEmail(), AdminRole.StoreStaff, SampleData.TestStoreId), TemporaryHash, TestContext.Current.CancellationToken);
        var operatorRole = await Service.AddAsync(Input(NextEmail(), AdminRole.Operator), TemporaryHash, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("storeIds", none.Error!.Errors!.Keys);
        Assert.Contains("storeIds", other.Error!.Errors!.Keys);
        Assert.Contains("role", operatorRole.Error!.Errors!.Keys);
    }

    // 役割と受け持つ店舗を替えると、資格の印も替わる (開いている管理画面をやり直させる)
    [Fact]
    public async Task UpdateChangesRoleAndSecurityStamp()
    {
        // Arrange
        using var scope = factory.BeginTenant(SampleData.DemoTenantId);
        var added = (await Service.AddAsync(Input(NextEmail(), AdminRole.StoreStaff, SampleData.DemoStoreId), TemporaryHash, TestContext.Current.CancellationToken)).Value!;
        var before = (await Account.FindAsync(added.Id, TestContext.Current.CancellationToken))!.SecurityStamp;

        // Act
        var error = await Service.UpdateAsync(added.Id, "管理者", AdminRole.TenantAdmin, [], added.Version, Guid.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(error);
        var item = Assert.Single(await Service.GetListAsync(TestContext.Current.CancellationToken), x => x.User.Id == added.Id);
        Assert.Equal(AdminRole.TenantAdmin, item.User.Role);
        Assert.Equal("管理者", item.User.Name);
        Assert.Empty(item.StoreIds);
        Assert.NotEqual(before, (await Account.FindAsync(added.Id, TestContext.Current.CancellationToken))!.SecurityStamp);

        // Act / Assert: 表示していた版が古ければ替えない
        var stale = await Service.UpdateAsync(added.Id, "古い", AdminRole.TenantAdmin, [], added.Version, Guid.Empty, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.VersionMismatch, stale?.ErrorCode);
    }

    // 自分の役割は替えられず、自分は止められない
    [Fact]
    public async Task CannotChangeOwnRoleOrDeactivateSelf()
    {
        // Arrange
        using var scope = factory.BeginTenant(SampleData.DemoTenantId);
        var self = (await Service.AddAsync(Input(NextEmail(), AdminRole.TenantAdmin), TemporaryHash, TestContext.Current.CancellationToken)).Value!;

        // Act
        var role = await Service.UpdateAsync(self.Id, self.Name, AdminRole.StoreStaff, [SampleData.DemoStoreId], self.Version, self.Id, TestContext.Current.CancellationToken);
        var active = await Service.SetActiveAsync(self.Id, false, self.Version, self.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("role", role!.Errors!.Keys);
        Assert.Contains("isActive", active!.Errors!.Keys);
    }

    // 止めた利用者はサインインできず、戻すとサインインできる
    [Fact]
    public async Task DeactivatedUserCannotSignIn()
    {
        // Arrange
        var email = NextEmail();
        AdminUserSummaryEntity added;
        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            added = (await Service.AddAsync(Input(email, AdminRole.TenantAdmin), TemporaryHash, TestContext.Current.CancellationToken)).Value!;

            // Act
            Assert.Null(await Service.SetActiveAsync(added.Id, false, added.Version, Guid.Empty, TestContext.Current.CancellationToken));
        }

        // Assert
        using (var admin = new TestAdmin(factory))
        {
            using var refused = await admin.SignInAsync(email, TemporaryPassword);
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        }

        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            Assert.Null(await Service.SetActiveAsync(added.Id, true, added.Version + 1, Guid.Empty, TestContext.Current.CancellationToken));
        }

        using var again = new TestAdmin(factory);
        using var signIn = await again.SignInAsync(email, TemporaryPassword);
        Assert.Equal("/account/password", TestAdmin.LocationOf(signIn));
    }

    // 仮のパスワードを出し直すと、新しい仮のパスワードでサインインでき、多要素を外すと鍵を捨てる
    [Fact]
    public async Task ResetPasswordAndTwoFactor()
    {
        // Arrange
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId);
        var user = (await Account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!;
        await using (var services = factory.Services.CreateAsyncScope())
        {
            var users = services.ServiceProvider.GetRequiredService<UserManager<AdminUserEntity>>();
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
        }

        // Act
        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            Assert.Null(await Service.ResetPasswordAsync(user.Id, TemporaryHash, TestContext.Current.CancellationToken));
            Assert.Null(await Service.ResetTwoFactorAsync(user.Id, TestContext.Current.CancellationToken));
        }

        // Assert
        var reset = (await Account.FindAsync(user.Id, TestContext.Current.CancellationToken))!;
        Assert.True(reset.MustChangePassword);
        Assert.False(reset.TwoFactorEnabled);
        Assert.Null(reset.AuthenticatorKey);
        using var admin = new TestAdmin(factory);
        using var signIn = await admin.SignInAsync(email, TemporaryPassword);
        Assert.Equal("/account/password", TestAdmin.LocationOf(signIn));
    }

    // ほかのテナントの利用者は、見つからないものとして替えない
    [Fact]
    public async Task OtherTenantUserIsNotFound()
    {
        // Arrange
        var (tenantId, _) = await factory.CreateTenantAsync();
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, tenantId);
        var other = (await Account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!;
        using var scope = factory.BeginTenant(SampleData.DemoTenantId);

        // Act
        var update = await Service.UpdateAsync(other.Id, "変更", AdminRole.TenantAdmin, [], other.Version, Guid.Empty, TestContext.Current.CancellationToken);
        var password = await Service.ResetPasswordAsync(other.Id, TemporaryHash, TestContext.Current.CancellationToken);
        var active = await Service.SetActiveAsync(other.Id, false, other.Version, Guid.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ErrorCodes.NotFound, update?.ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, password?.ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, active?.ErrorCode);
        Assert.DoesNotContain(await Service.GetListAsync(TestContext.Current.CancellationToken), x => x.User.Id == other.Id);
    }

    private string NextEmail() => $"added{Interlocked.Increment(ref lastUser)}-{Guid.NewGuid():N}@users.example.com";

    private static AdminUserInput Input(string email, AdminRole role, params Guid[] storeIds) =>
        new(email, email.ToUpperInvariant(), "利用者", role, storeIds);
}
