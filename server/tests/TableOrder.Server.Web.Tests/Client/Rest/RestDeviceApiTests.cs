namespace TableOrder.Server.Web.Client.Rest;

using TableOrder.Client;
using TableOrder.Contract.Devices;

// 端末のアプリの共通の REST の窓口 (登録、トークン、状態の報告、端末の設定) を、本物のサーバにつないで確かめる
public sealed class RestDeviceApiTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public RestDeviceApiTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 登録してトークンを受け取り、状態を報告して、端末の設定で置いたテーブルを読める
    [Fact]
    public async Task PairAndReadConfig()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var paired = await terminal.PairAsync(store.TableCodes[0]);
        var authenticated = await terminal.Device.AuthenticateAsync(cancel);
        var reported = await terminal.Device.ReportStatusAsync(new DeviceHeartbeatRequest { AppVersion = "1.0.0", BatteryLevel = 0.5m, IsCharging = true }, cancel);
        var config = await terminal.Device.GetConfigAsync(cancel);

        // Assert
        Assert.Equal(DeviceKind.Table, paired.Kind);
        Assert.True(authenticated.IsSuccess);
        Assert.True(reported.IsSuccess);
        Assert.Equal(store.TableIds[0], config.Content!.Device!.TableId);
    }

    // 画像は中身をそのまま受け取り、置いていない名前は断られる
    [Fact]
    public async Task GetImageReturnsContent()
    {
        // Arrange
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(SampleData.DemoTableCode);
        var cancel = TestContext.Current.CancellationToken;
        var name = (await terminal.Table.GetMenuAsync(cancel)).Content!.Items.First(static x => x.ImageName is not null).ImageName!;

        // Act
        var image = await terminal.Device.GetImageAsync(name, cancel);
        var unknown = await terminal.Device.GetImageAsync("unknown.12345678.png", cancel);

        // Assert
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine("Assets", "Images", name), cancel), image.Content);
        Assert.Equal(ApiStatus.Rejected, unknown.Status);
        Assert.Equal("NOT_FOUND", unknown.ErrorCode);
    }

    // 無効にされた端末は、トークンの要求で断られて Denied で知らせる
    [Fact]
    public async Task RevokedDeviceIsDenied()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.PairAsync(store.TableCodes[0]);
        await factory.RevokeDeviceAsync(paired.DeviceId);

        // Act
        var result = await terminal.Device.AuthenticateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ApiStatus.Unauthorized, result.Status);
        Assert.Equal("DEVICE_REVOKED", result.ErrorCode);
        var denied = await terminal.NextDeniedAsync();
        Assert.Equal(paired.DeviceId, denied.DeviceId);
        Assert.Equal(DeviceDenial.Revoked, denied.Reason);
    }

    // テナントの停止で断られたあとは、起動の取り直し (AuthenticateAsync) のほかはトークンを求めずに断りを返し (止めている間に要求を送り続けない)、取り直せたら戻る
    [Fact]
    public async Task SuspendedTenantStopsTokenRequestsUntilAuthenticated()
    {
        // Arrange
        var (tenantId, code) = await factory.CreateTenantAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(code);
        var cancel = TestContext.Current.CancellationToken;
        var heartbeat = new DeviceHeartbeatRequest { AppVersion = "1.0.0" };
        await factory.SuspendTenantAsync(tenantId);

        // Act / Assert: 止められていると断られ、Denied で知らせる
        var suspended = await terminal.Device.AuthenticateAsync(cancel);
        Assert.Equal("TENANT_SUSPENDED", suspended.ErrorCode);
        Assert.Equal(DeviceDenial.TenantSuspended, (await terminal.NextDeniedAsync()).Reason);

        // Act / Assert: テナントを戻しても、起動で取り直すまではトークンを求めずに止まっているとして返す
        await factory.ResumeTenantAsync(tenantId);
        var blocked = await terminal.Device.ReportStatusAsync(heartbeat, cancel);
        Assert.Equal(ApiStatus.Unauthorized, blocked.Status);
        Assert.Equal("TENANT_SUSPENDED", blocked.ErrorCode);

        // Act / Assert: 起動で取り直すと戻る
        Assert.True((await terminal.Device.AuthenticateAsync(cancel)).IsSuccess);
        Assert.True((await terminal.Device.ReportStatusAsync(heartbeat, cancel)).IsSuccess);
    }

    // 登録していない端末と、URL でない接続先は通信しない
    [Fact]
    public async Task UnregisteredOrInvalidEndPointDoesNotSend()
    {
        // Arrange
        await using var terminal = TestTerminal.Create(factory);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var unregistered = await terminal.Device.GetConfigAsync(cancel);
        terminal.Context.ApiEndPoint = "not a url";
        var invalid = await terminal.Device.PairAsync(new DevicePairRequest { PairingCode = "000000", DeviceName = "test" }, cancel);

        // Assert
        Assert.Equal(ApiStatus.Unauthorized, unregistered.Status);
        Assert.Equal(ApiStatus.Unavailable, invalid.Status);
    }
}
