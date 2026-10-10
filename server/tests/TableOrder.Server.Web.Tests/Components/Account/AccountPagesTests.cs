namespace TableOrder.Server.Web.Components.Account;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Models.Enums;
using TableOrder.Server.Core.Services;

public sealed class AccountPagesTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public AccountPagesTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private AccountService Account => factory.Services.GetRequiredService<AccountService>();

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

    // 同時に間違えたサインインも 1 回ずつ数え、5 回で止める (資格情報の版の違いで数え漏らさない)
    [Fact]
    public async Task ConcurrentWrongPasswordsLockOut()
    {
        // Arrange
        var email = await factory.CreateAdminUserAsync(AdminRole.Operator, null);
        var admins = Enumerable.Range(0, 5).Select(_ => new TestAdmin(factory)).ToList();

        // Act
        try
        {
            foreach (var attempt in await Task.WhenAll(admins.Select((x, i) => x.SignInAsync(email, $"wrong-password-{i}"))))
            {
                attempt.Dispose();
            }
        }
        finally
        {
            admins.ForEach(static x => x.Dispose());
        }

        using var admin = new TestAdmin(factory);
        using var locked = await admin.SignInAsync(email, ServerFactory.AdminPassword);

        // Assert
        Assert.Contains("しばらくサインインできません", await TestAdmin.ReadPageAsync(locked), StringComparison.Ordinal);
    }

    // サインインの送信は、接続元ごとに 1 分の回数を超えると断る (画面を開くのは限らない)
    [Fact]
    public async Task SignInPostsAreRateLimited()
    {
        // Arrange: サインインの回数を 1 分に 3 回にしたサーバ
        await using var server = new ServerFactory();
        server.Settings["RateLimit:SignInPerMinute"] = "3";
        using var admin = new TestAdmin(server);

        // Act
        for (var i = 0; i < 3; i++)
        {
            using var attempt = await admin.SignInAsync("nobody@example.com", $"wrong-password-{i}");
        }

        using var limited = await admin.SignInAsync("nobody@example.com", "wrong-password-3");
        using var page = await admin.GetAsync("/account/sign-in");

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
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

    // 仮のパスワードの利用者は、パスワードを替えてから管理画面に進む (アカウントと多要素の画面も開かず、仮のパスワードのままには替えられない)
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

        // Act / Assert: アカウントと多要素の画面からもパスワードの画面に移る
        using (var account = await admin.GetAsync("/account"))
        {
            Assert.Equal("/account/password", TestAdmin.LocationOf(account));
        }

        using (var twoFactor = await admin.GetAsync("/account/two-factor"))
        {
            Assert.Equal("/account/password", TestAdmin.LocationOf(twoFactor));
        }

        // Act / Assert: 仮のパスワードのままには替えない
        using (var same = await admin.PostFormAsync("/account/password", "password", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = ServerFactory.AdminPassword,
            ["Input.NewPassword"] = ServerFactory.AdminPassword,
            ["Input.ConfirmPassword"] = ServerFactory.AdminPassword
        }))
        {
            Assert.Equal(HttpStatusCode.OK, same.StatusCode);
            Assert.Null(TestAdmin.LocationOf(same));
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

    // 回復用のコードは 1 回だけ使え、DB にはコードのまま持たない
    [Fact]
    public async Task RecoveryCodeSignsInOnce()
    {
        // Arrange
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId);
        await EnableTwoFactorAsync(email);
        string code;
        string? stored;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUserEntity>>();
            var user = (await users.FindByNameAsync(email))!;
            code = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 3))!.First();
            stored = (await users.FindByNameAsync(email))!.RecoveryCodes;
        }

        // Act / Assert: 回復用のコードでサインインする
        using (var admin = new TestAdmin(factory))
        {
            using var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword);
            using var recovered = await admin.PostFormAsync("/account/sign-in-2fa?returnUrl=%2F", "recovery", new Dictionary<string, string> { ["Recovery.Code"] = code });
            Assert.Equal("/", TestAdmin.LocationOf(recovered));
        }

        // Act / Assert: 使ったコードは 2 回目には使えない
        using (var again = new TestAdmin(factory))
        {
            using var signIn = await again.SignInAsync(email, ServerFactory.AdminPassword);
            using var reused = await again.PostFormAsync("/account/sign-in-2fa?returnUrl=%2F", "recovery", new Dictionary<string, string> { ["Recovery.Code"] = code });
            Assert.Null(TestAdmin.LocationOf(reused));
        }

        Assert.NotNull(stored);
        Assert.DoesNotContain(code, stored, StringComparison.Ordinal);
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

    // 止めた利用者は、止める前のサインインのままではパスワードを替えられない (新しい資格の印で Cookie を出し直させない)
    [Fact]
    public async Task DeactivatedUserCannotChangePasswordWithEarlierSignIn()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, SampleData.DemoTenantId);
        using (var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword))
        {
            Assert.Equal("/", TestAdmin.LocationOf(signIn));
        }

        var user = (await Account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!;
        using (factory.BeginTenant(SampleData.DemoTenantId))
        {
            Assert.Null(await factory.Services.GetRequiredService<AdminUserService>().SetActiveAsync(user.Id, false, user.Version, Guid.Empty, TestContext.Current.CancellationToken));
        }

        // Act
        using var response = await admin.PostFormAsync("/account/password", "password", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = ServerFactory.AdminPassword,
            ["Input.NewPassword"] = "changed-password-1",
            ["Input.ConfirmPassword"] = "changed-password-1"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("パスワードを替えました。", await TestAdmin.ReadPageAsync(response), StringComparison.Ordinal);
        var stored = (await Account.FindByEmailAsync(email.ToUpperInvariant(), TestContext.Current.CancellationToken))!;
        Assert.Equal(user.PasswordHash, stored.PasswordHash);
    }

    // 止めたテナントの利用者がパスワードを替えると、Cookie を出し直さずにサインアウトさせる
    [Fact]
    public async Task SuspendedTenantUserIsSignedOutAfterPasswordChange()
    {
        // Arrange
        using var admin = new TestAdmin(factory);
        var (tenantId, _) = await factory.CreateTenantAsync();
        var email = await factory.CreateAdminUserAsync(AdminRole.TenantAdmin, tenantId);
        using (var signIn = await admin.SignInAsync(email, ServerFactory.AdminPassword))
        {
            Assert.Equal("/", TestAdmin.LocationOf(signIn));
        }

        await factory.SuspendTenantAsync(tenantId);

        // Act
        using var response = await admin.PostFormAsync("/account/password", "password", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = ServerFactory.AdminPassword,
            ["Input.NewPassword"] = "changed-password-1",
            ["Input.ConfirmPassword"] = "changed-password-1"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var page = await admin.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/account/sign-in", TestAdmin.LocationOf(page), StringComparison.Ordinal);
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
