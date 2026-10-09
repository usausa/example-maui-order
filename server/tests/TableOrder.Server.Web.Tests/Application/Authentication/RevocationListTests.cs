namespace TableOrder.Server.Web.Application.Authentication;

using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Server.Core.Services;

// すぐに拒む一覧 (無効にした端末と止めたテナントのアクセストークンを、期限の前でも断る)
public sealed class RevocationListTests : IClassFixture<ServerFactory>
{
    private const string ConfigPath = "/api/v1/devices/me/config";

    private readonly ServerFactory factory;

    public RevocationListTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 無効にした端末の今のトークンは、すぐに 401 にする。トークンの取り直しは DEVICE_REVOKED で断る
    [Fact]
    public async Task RevokedDeviceTokenIsRejectedImmediately()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(store.TableCodes[0]);
        var devices = factory.Services.GetRequiredService<DeviceService>();
        using var before = await GetConfigAsync(device);

        // Act
        using (factory.BeginStore(store))
        {
            var version = (await devices.GetSummaryListAsync(cancel)).Single(x => x.Device.Id == device.DeviceId).Device.Version;
            Assert.Null(await devices.RevokeAsync(device.DeviceId, version, cancel));
        }

        using var after = await GetConfigAsync(device);
        using var token = await device.RequestTokenAsync(device.CreateAssertion());

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, token.StatusCode);
        Assert.Equal("DEVICE_REVOKED", await TestDevice.ReadErrorCodeAsync(token));
    }

    // 止めたテナントの端末のトークンは、すぐに 401 にする。戻すと同じトークンが通る
    [Fact]
    public async Task SuspendedTenantTokenIsRejectedUntilResumed()
    {
        // Arrange
        var (tenantId, code) = await factory.CreateTenantAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        var tenants = factory.Services.GetRequiredService<TenantService>();
        var version = (await tenants.GetAsync(tenantId, cancel))!.Version;

        // Act
        using (factory.BeginTenant(null))
        {
            Assert.Null(await tenants.SetSuspendedAsync(tenantId, true, version, cancel));
        }

        using var suspended = await GetConfigAsync(device);
        using (factory.BeginTenant(null))
        {
            Assert.Null(await tenants.SetSuspendedAsync(tenantId, false, version + 1, cancel));
        }

        using var resumed = await GetConfigAsync(device);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, suspended.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
    }

    // ほかのサーバで無効にした端末 (この一覧を読み直さずに DB だけが替わる) も、読み直しの間隔のうちに断る
    [Fact]
    public async Task SweepRejectsDeviceRevokedElsewhere()
    {
        // Arrange: 読み直しの間隔を 1 秒にしたサーバ
        await using var server = new ServerFactory();
        server.Settings["Token:RevocationSweepSeconds"] = "1";
        var store = await server.CreateStoreAsync();
        using var device = new TestDevice(server.CreateClient());
        await device.SignInAsync(store.TableCodes[0]);

        // Act
        await server.RevokeDeviceAsync(device.DeviceId, refresh: false);
        var rejected = await WaitRejectedAsync(device, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(rejected);
    }

    private static Task<HttpResponseMessage> GetConfigAsync(TestDevice device) =>
        device.Client.GetAsync(new Uri(ConfigPath, UriKind.Relative), TestContext.Current.CancellationToken);

    // 401 になるまで送り直す (時間を区切る)
    private static async Task<bool> WaitRejectedAsync(TestDevice device, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            using var response = await GetConfigAsync(device);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return true;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
