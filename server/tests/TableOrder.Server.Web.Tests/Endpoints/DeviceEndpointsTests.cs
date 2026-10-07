namespace TableOrder.Server.Web.Endpoints;

using System.Security.Cryptography;
using System.Text.Json;

using TableOrder.Contract.Devices;

public sealed class DeviceEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public DeviceEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    //--------------------------------------------------------------------------------
    // Pair
    //--------------------------------------------------------------------------------

    // ペアリングコードで登録すると、コードの店舗と種類の端末になる
    [Fact]
    public async Task PairWithPairingCode()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());

        // Act
        using var response = await device.PairAsync(SampleData.DemoTableCode);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var paired = await response.Content.ReadFromJsonAsync<DevicePairResponse>(TestDevice.JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(DeviceKind.Table, paired!.Kind);
        Assert.Equal(SampleData.DemoStoreId, paired.StoreId);
    }

    // 知らないコードは登録しない
    [Fact]
    public async Task PairRejectsUnknownCode()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());

        // Act
        using var response = await device.PairAsync("999999");

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PAIRING_CODE_INVALID", await ReadErrorCodeAsync(response));
    }

    // 曲線の上の点でない公開鍵は受けない
    [Fact]
    public async Task PairRejectsInvalidPublicKey()
    {
        // Arrange
        using var client = factory.CreateClient();
        var request = new DevicePairRequest
        {
            PairingCode = SampleData.DemoTableCode,
            PublicKey = new DevicePublicKey { Kty = "EC", Crv = "P-256", X = new string('A', 43), Y = new string('A', 43) },
            DeviceName = "test"
        };

        // Act
        using var response = await client.PostAsJsonAsync("/api/v1/devices/pair", request, TestDevice.JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCodeAsync(response));
    }

    //--------------------------------------------------------------------------------
    // Token
    //--------------------------------------------------------------------------------

    // 端末の鍵で署名した要求にアクセストークンを出す (期限 30 分)
    [Fact]
    public async Task TokenIsIssuedForSignedAssertion()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);

        // Act
        using var response = await device.RequestTokenAsync(device.CreateAssertion());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<DeviceTokenResponse>(TestDevice.JsonOptions, TestContext.Current.CancellationToken);
        Assert.False(String.IsNullOrEmpty(token!.AccessToken));
        Assert.Equal(1800, token.ExpiresIn);
    }

    // ほかの鍵で署名した要求は 401 (端末があるかどうかを見せない)
    [Fact]
    public async Task TokenRejectsAssertionSignedByOtherKey()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        using var response = await device.RequestTokenAsync(device.CreateAssertion(other));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // 同じ要求 (jti) は 2 回受けない
    [Fact]
    public async Task TokenRejectsReusedAssertion()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);
        var assertion = device.CreateAssertion();

        // Act
        using var first = await device.RequestTokenAsync(assertion);
        using var second = await device.RequestTokenAsync(assertion);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    // 無効にした端末には、次のトークンを出さない
    [Fact]
    public async Task TokenRejectsRevokedDevice()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);
        await factory.RevokeDeviceAsync(device.DeviceId);

        // Act
        using var response = await device.RequestTokenAsync(device.CreateAssertion());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_REVOKED", await ReadErrorCodeAsync(response));
    }

    // 契約を止めたテナントの端末には、トークンを出さず、登録もさせない
    [Fact]
    public async Task TokenRejectsSuspendedTenant()
    {
        // Arrange: このテストだけのテナントを作る (ほかのテストのテナントを止めない)
        var (tenantId, code) = await factory.CreateTenantAsync();
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        await factory.SuspendTenantAsync(tenantId);

        // Act
        using var token = await device.RequestTokenAsync(device.CreateAssertion());
        using var other = new TestDevice(factory.CreateClient());
        using var pair = await other.PairAsync(code);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, token.StatusCode);
        Assert.Equal("TENANT_SUSPENDED", await ReadErrorCodeAsync(token));
        Assert.Equal(HttpStatusCode.Forbidden, pair.StatusCode);
        Assert.Equal("TENANT_SUSPENDED", await ReadErrorCodeAsync(pair));
    }

    //--------------------------------------------------------------------------------
    // Me
    //--------------------------------------------------------------------------------

    // 端末の設定に、店舗の設定と、端末の置き場所 (テーブル) を返す
    [Fact]
    public async Task ConfigReturnsStoreSettingsAndDevice()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);

        // Act
        var config = await device.GetAsync<DeviceConfigResponse>("/api/v1/devices/me/config");

        // Assert
        Assert.Equal("駅前店", config.StoreName.Ja);
        Assert.Equal(["ja", "en"], config.Languages);
        Assert.Equal(7, config.CallReasons.Count);
        Assert.Equal(DeviceKind.Table, config.Device!.Kind);
        Assert.Equal(device.DeviceId, config.Device.Id);
        Assert.Equal("1", config.Device.TableName);
    }

    // キッチン端末は、登録したときの持ち場を受け持つ
    [Fact]
    public async Task ConfigReturnsKitchenStations()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoKitchenCode);

        // Act
        var config = await device.GetAsync<DeviceConfigResponse>("/api/v1/devices/me/config");

        // Assert
        Assert.Equal(DeviceKind.Kitchen, config.Device!.Kind);
        Assert.Equal(3, config.Device.StationIds.Count);
    }

    // 状態の報告を受ける
    [Fact]
    public async Task HeartbeatIsAccepted()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);

        // Act
        using var response = await device.Client.PostAsJsonAsync(
            "/api/v1/devices/me/heartbeat",
            new DeviceHeartbeatRequest { AppVersion = "1.0.1", BatteryLevel = 0.5m, IsCharging = true },
            TestDevice.JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // トークンのない要求は 401
    [Fact]
    public async Task RequestWithoutTokenIsUnauthorized()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/api/v1/devices/me/config", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.TryGetProperty("errorCode", out var value) ? value.GetString() : null;
    }
}
