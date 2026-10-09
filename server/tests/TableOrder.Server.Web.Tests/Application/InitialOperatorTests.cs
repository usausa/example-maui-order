namespace TableOrder.Server.Web.Application;

public sealed class InitialOperatorTests
{
    // 運営者がいなければ、起動のときに設定のメールアドレスと仮のパスワードで作り、次のサインインでパスワードを替えさせる
    [Fact]
    public async Task InitialOperatorIsCreatedFromSettings()
    {
        // Arrange
        await using var factory = new ServerFactory();
        factory.Settings["Database:SampleData"] = "false";
        factory.Settings["Admin:InitialOperatorEmail"] = "first@example.com";
        factory.Settings["Admin:InitialOperatorPassword"] = "first-operator-password";
        using var admin = new TestAdmin(factory);

        // Act
        using var response = await admin.SignInAsync("first@example.com", "first-operator-password");

        // Assert
        Assert.Equal("/account/password", TestAdmin.LocationOf(response));
    }

    // パスワードの決まり (12 文字以上) に合わない仮のパスワードでは作らない
    [Fact]
    public async Task InitialOperatorRequiresValidPassword()
    {
        // Arrange
        await using var factory = new ServerFactory();
        factory.Settings["Database:SampleData"] = "false";
        factory.Settings["Admin:InitialOperatorEmail"] = "first@example.com";
        factory.Settings["Admin:InitialOperatorPassword"] = "short";
        using var admin = new TestAdmin(factory);

        // Act
        using var response = await admin.SignInAsync("first@example.com", "short");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
