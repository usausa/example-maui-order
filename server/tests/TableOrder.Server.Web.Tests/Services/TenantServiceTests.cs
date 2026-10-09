namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Enums;
using TableOrder.Server.Core.Services;

public sealed class TenantServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public TenantServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private TenantService Service => factory.Services.GetRequiredService<TenantService>();

    // テナントを登録すると一覧に出る。コードはすべてのテナントで一意で、決まった形にする
    [Fact]
    public async Task CreateAddsTenant()
    {
        // Arrange
        var code = $"t{Guid.NewGuid():N}"[..20];
        using var scope = factory.BeginTenant(null);

        // Act
        var created = await Service.CreateAsync(code, "テスト", new LocalizedText { Ja = "チェーン", En = "Chain" }, TestContext.Current.CancellationToken);
        var duplicate = await Service.CreateAsync(code, "テスト", new LocalizedText { Ja = "チェーン" }, TestContext.Current.CancellationToken);
        var invalid = await Service.CreateAsync("Bad Code", "テスト", new LocalizedText { Ja = "チェーン" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(created.Succeeded);
        var summary = Assert.Single(await Service.GetSummaryListAsync(TestContext.Current.CancellationToken), x => x.Id == created.Value.Id);
        Assert.Equal(TenantStatus.Active, summary.Status);
        Assert.Equal(0, summary.StoreCount);
        Assert.Equal("Chain", summary.BrandName.En);
        Assert.Contains("code", duplicate.Error!.Errors!.Keys);
        Assert.Contains("code", invalid.Error!.Errors!.Keys);
    }

    // 止めたテナントは端末を登録できず、利用者の資格の印が替わってサインインできない。戻すと使える
    [Fact]
    public async Task SuspendStopsDevicesAndUsers()
    {
        // Arrange
        var (tenantId, code) = await factory.CreateTenantAsync();
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, tenantId);
        var account = factory.Services.GetRequiredService<AccountService>();
        var stamp = (await account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!.SecurityStamp;
        var version = await TenantVersionAsync(tenantId);

        // Act
        using (factory.BeginTenant(null))
        {
            Assert.Null(await Service.SetSuspendedAsync(tenantId, true, version, TestContext.Current.CancellationToken));
        }

        // Assert
        Assert.NotEqual(stamp, (await account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!.SecurityStamp);
        using (var device = new TestDevice(factory.CreateClient()))
        {
            using var pair = await device.PairAsync(code);
            Assert.Equal(HttpStatusCode.Forbidden, pair.StatusCode);
        }

        using (var admin = new TestAdmin(factory))
        {
            using var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword);
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        }

        // Act / Assert: 戻すと登録とサインインができる
        using (factory.BeginTenant(null))
        {
            Assert.Null(await Service.SetSuspendedAsync(tenantId, false, version + 1, TestContext.Current.CancellationToken));
        }

        using var resumed = new TestDevice(factory.CreateClient());
        using var paired = await resumed.PairAsync(code);
        Assert.Equal(HttpStatusCode.Created, paired.StatusCode);
        using var again = new TestAdmin(factory);
        using var signedIn = await again.SignInAsync(email, ServerFactory.AdminPassword);
        Assert.Equal("/", TestAdmin.LocationOf(signedIn));
    }

    // 表示していた版が古ければ替えない
    [Fact]
    public async Task SuspendRejectsStaleVersion()
    {
        // Arrange
        var (tenantId, _) = await factory.CreateTenantAsync();
        var version = await TenantVersionAsync(tenantId);
        using var scope = factory.BeginTenant(null);

        // Act
        var error = await Service.SetSuspendedAsync(tenantId, true, version + 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ErrorCodes.VersionMismatch, error?.ErrorCode);
    }

    private async ValueTask<int> TenantVersionAsync(Guid tenantId) =>
        (await Service.GetAsync(tenantId, TestContext.Current.CancellationToken))!.Version;
}
