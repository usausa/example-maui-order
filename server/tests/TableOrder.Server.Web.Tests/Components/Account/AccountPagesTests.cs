namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Models.Enums;

public sealed class AccountPagesTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public AccountPagesTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // サインインしていなければ、管理画面はサインインの画面に移る (戻る先を付ける)
    [Fact]
    public async Task AdminPagesRedirectToSignIn()
    {
        // Arrange
        using var admin = new TestAdmin(factory);

        // Act
        using var response = await admin.GetAsync("/devices");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/sign-in?ReturnUrl=%2Fdevices", TestAdmin.LocationOf(response));
    }

    // サンプルの利用者でサインインすると、戻る先に移って管理画面を開ける
    [Fact]
    public async Task SignInOpensAdminPages()
    {
        // Arrange
        using var admin = new TestAdmin(factory);

        // Act
        using var response = await admin.PostFormAsync("/account/sign-in?ReturnUrl=%2Fdevices", "sign-in", new Dictionary<string, string>
        {
            ["Input.Email"] = "operator@example.com",
            ["Input.Password"] = "tableorder-dev"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/devices", TestAdmin.LocationOf(response));
        using var page = await admin.GetAsync("/devices");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    // ほかのサイトの戻る先は使わない
    [Fact]
    public async Task SignInIgnoresExternalReturnUrl()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.Operator, null);

        // Act
        using var response = await admin.PostFormAsync("/account/sign-in?ReturnUrl=%2F%2Fexample.com", "sign-in", new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = ServerFactory.AdminPassword
        });

        // Assert
        Assert.Equal("/", TestAdmin.LocationOf(response));
    }

    // パスワードを間違えると、どこを間違えたかを分けずに知らせ、5 回続けて間違えると止める
    [Fact]
    public async Task WrongPasswordLocksOutAfterFiveAttempts()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.Operator, null);

        // Act / Assert: 間違えたことだけを知らせる
        using (var wrong = await admin.SignInAsync(email, "wrong-password-0"))
        {
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains("メールアドレスかパスワードが違います。", await TestAdmin.ReadPageAsync(wrong), StringComparison.Ordinal);
        }

        // Act / Assert: 5 回目で止め、正しいパスワードでもサインインできない
        for (var i = 1; i < 5; i++)
        {
            using var attempt = await admin.SignInAsync(email, $"wrong-password-{i}");
        }

        using var locked = await admin.SignInAsync(email, ServerFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("しばらくサインインできません", await TestAdmin.ReadPageAsync(locked), StringComparison.Ordinal);
    }

    // 店舗の担当は、テナントの管理者の画面 (チェーン) を開けない
    [Fact]
    public async Task StoreStaffCannotOpenTenantAdminPages()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.StoreStaff, SampleData.DemoTenantId, [SampleData.DemoStoreId]);
        using var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword);

        // Act
        using var response = await admin.GetAsync("/brand");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/access-denied?ReturnUrl=%2Fbrand", TestAdmin.LocationOf(response));
    }

    // 仮のパスワードの利用者は、パスワードを替えてから管理画面に進む
    [Fact]
    public async Task TemporaryPasswordMustBeChanged()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId, mustChangePassword: true);

        // Act / Assert: サインインするとパスワードの画面に移る
        using (var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword))
        {
            Assert.Equal("/account/password", TestAdmin.LocationOf(signIn));
        }

        // Act / Assert: 替えると管理画面に移る
        using (var changed = await admin.PostFormAsync("/account/password", "password", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = ServerFactory.AdminPassword,
            ["Input.NewPassword"] = "changed-password-1",
            ["Input.ConfirmPassword"] = "changed-password-1"
        }))
        {
            Assert.Equal("/", TestAdmin.LocationOf(changed));
        }

        // Act / Assert: 次からは新しいパスワードでサインインし、仮のパスワードの画面には移らない
        using var again = new TestAdmin(factory);
        using var next = await again.SignInAsync(email, "changed-password-1");
        Assert.Equal("/", TestAdmin.LocationOf(next));
    }

    // 多要素を使う利用者は、パスワードのあとに認証アプリのコードを入れる
    [Fact]
    public async Task TwoFactorRequiresAuthenticatorCode()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId);
        var key = await EnableTwoFactorAsync(email);

        // Act / Assert: パスワードのあとにコードの画面に移る
        using (var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword))
        {
            Assert.Equal("/account/sign-in-2fa?returnUrl=%2F", TestAdmin.LocationOf(signIn));
        }

        // Act / Assert: 違うコードは断る
        using (var wrong = await admin.PostFormAsync("/account/sign-in-2fa?returnUrl=%2F", "code", new Dictionary<string, string> { ["Code.Code"] = "000000" }))
        {
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains("コードが違います。", await TestAdmin.ReadPageAsync(wrong), StringComparison.Ordinal);
        }

        // Act / Assert: 認証アプリのコードでサインインする
        using var verified = await admin.PostFormAsync("/account/sign-in-2fa?returnUrl=%2F", "code", new Dictionary<string, string> { ["Code.Code"] = TestTotp.Compute(key, DateTimeOffset.UtcNow) });
        Assert.Equal("/", TestAdmin.LocationOf(verified));
        using var page = await admin.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    // 止めた利用者と、止めたテナントの利用者はサインインできない (理由は分けない)
    [Fact]
    public async Task InactiveUserAndSuspendedTenantCannotSignIn()
    {
        // Arrange
        var inactive = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId, isActive: false);
        var (tenantId, _) = await factory.CreateTenantAsync();
        var suspended = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, tenantId);
        await factory.SuspendTenantAsync(tenantId);

        // Act / Assert
        await AssertSignInRefusedAsync(inactive);
        await AssertSignInRefusedAsync(suspended);
    }

    // サインアウトすると、管理画面はサインインの画面に戻る
    [Fact]
    public async Task SignOutEndsSession()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.Operator, null);
        using var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword);

        // Act
        using var signOut = await admin.PostFormAsync("/account", string.Empty, new Dictionary<string, string>(), "/account/sign-out");

        // Assert
        Assert.Equal("/account/sign-in", TestAdmin.LocationOf(signOut));
        using var page = await admin.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
    }

    // サインアウトは、ほかのサイトからの要求 (偽造防止の値がない) を受けない
    [Fact]
    public async Task SignOutRequiresAntiforgeryToken()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.Operator, null);
        using var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword);

        // Act
        using var signOut = await admin.PostWithoutTokenAsync("/account/sign-out");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, signOut.StatusCode);
        using var page = await admin.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    private async ValueTask AssertSignInRefusedAsync(string email)
    {
        using var admin = new TestAdmin(factory);
        using var response = await admin.SignInAsync(email, ServerFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("メールアドレスかパスワードが違います。", await TestAdmin.ReadPageAsync(response), StringComparison.Ordinal);
    }

    private async ValueTask<string> EnableTwoFactorAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUserEntity>>();
        var user = await users.FindByNameAsync(email);
        Assert.NotNull(user);
        await users.ResetAuthenticatorKeyAsync(user);
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GetAuthenticatorKeyAsync(user))!;
    }
}
