namespace TableOrder.Server.Web.Endpoints;

using System.Net.Http.Headers;
using System.Security.Cryptography;

using TableOrder.Client;
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
        Assert.Equal("PAIRING_CODE_INVALID", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 登録の回数を超えた接続元は 429 にし、送り直せる時刻を Retry-After で知らせる
    [Fact]
    public async Task PairIsRateLimitedWithRetryAfter()
    {
        // Arrange: 登録の回数を 1 分に 2 回にしたサーバ
        await using var server = new ServerFactory();
        server.Settings["RateLimit:PairingPerMinute"] = "2";
        using var device = new TestDevice(server.CreateClient());

        // Act
        for (var i = 0; i < 2; i++)
        {
            using var attempt = await device.PairAsync("999999");
        }

        using var limited = await device.PairAsync("999999");

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    // 種類の違うアプリの登録は 422 (コードは使わない)
    [Fact]
    public async Task PairRejectsOtherKind()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());

        // Act
        using var response = await device.PairAsync(SampleData.DemoTableCode, DeviceKind.Hall);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("DEVICE_KIND_MISMATCH", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 登録するアプリの端末の種類のない要求は受けない
    [Fact]
    public async Task PairRequiresKind()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        var request = new DevicePairRequest { PairingCode = SampleData.DemoTableCode, PublicKey = device.PublicKey, DeviceName = "test" };

        // Act
        using var response = await device.PostAsync("/api/v1/devices/pair", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
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
            Kind = DeviceKind.Table,
            PublicKey = new DevicePublicKey { Kty = "EC", Crv = "P-256", X = new string('A', 43), Y = new string('A', 43) },
            DeviceName = "test"
        };

        // Act
        using var response = await client.PostAsJsonAsync("/api/v1/devices/pair", request, TestDevice.JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
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

    // 端末のアプリと同じ形の鍵 (公開鍵は SubjectPublicKeyInfo、署名は DER) を、端末のアプリの変換 (DeviceCredentials) で送って登録し、トークンを受け取れる
    [Fact]
    public async Task TokenIsIssuedForDeviceCredentials()
    {
        // Arrange
        using var client = factory.CreateClient();
        var key = new TestDeviceKey();
        var request = new DevicePairRequest
        {
            PairingCode = SampleData.DemoTableCode,
            Kind = DeviceKind.Table,
            PublicKey = DeviceCredentials.CreatePublicKey(await key.GetPublicKeyAsync()),
            DeviceName = "test"
        };
        using var pair = await client.PostAsJsonAsync("/api/v1/devices/pair", request, TestDevice.JsonOptions, TestContext.Current.CancellationToken);
        var device = await pair.Content.ReadFromJsonAsync<DevicePairResponse>(TestDevice.JsonOptions, TestContext.Current.CancellationToken);

        // Act
        var assertion = await DeviceCredentials.CreateAssertionAsync(key, device!.DeviceId, DateTimeOffset.UtcNow);
        using var response = await client.PostAsJsonAsync("/api/v1/devices/token", new DeviceTokenRequest { Assertion = assertion }, TestDevice.JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        Assert.Equal("DEVICE_REVOKED", await TestDevice.ReadErrorCodeAsync(response));
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
        Assert.Equal("TENANT_SUSPENDED", await TestDevice.ReadErrorCodeAsync(token));
        Assert.Equal(HttpStatusCode.Forbidden, pair.StatusCode);
        Assert.Equal("TENANT_SUSPENDED", await TestDevice.ReadErrorCodeAsync(pair));
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

    // チェーンの設定 (名前・ロゴ・色) と店舗の設定 (機能、来店の開き方、スタッフの PIN、言語、支払方法) は、テナントと店舗ごとに返る
    [Fact]
    public async Task ConfigReturnsChainAndStoreSettings()
    {
        // Arrange
        using var demo = new TestDevice(factory.CreateClient());
        await demo.SignInAsync(SampleData.DemoTableCode);
        using var test = new TestDevice(factory.CreateClient());
        await test.SignInAsync(SampleData.TestTableCode);

        // Act
        var demoConfig = await demo.GetAsync<DeviceConfigResponse>("/api/v1/devices/me/config");
        var testConfig = await test.GetAsync<DeviceConfigResponse>("/api/v1/devices/me/config");

        // Assert
        Assert.Equal("バニーズ", demoConfig.Brand.Name.Ja);
        Assert.StartsWith("logo-bunnys.", demoConfig.Brand.LogoImageName, StringComparison.Ordinal);
        Assert.Empty(demoConfig.Brand.Theme);
        Assert.True(demoConfig.Features.RegisterCheckout);
        Assert.True(demoConfig.Features.SplitPayment);
        Assert.Equal((30, 30), (demoConfig.Features.LastOrderNoticeMinutes, demoConfig.Features.FinishSeconds));
        Assert.Equal(VisitOpening.Hall, demoConfig.Features.VisitOpening);
        Assert.Equal(15, demoConfig.Features.KitchenAlertMinutes);
        Assert.True(StaffPins.Verify("1234", demoConfig.StaffPin!.Iterations, demoConfig.StaffPin.Salt, demoConfig.StaffPin.Hash));

        Assert.Equal("さつき軒", testConfig.Brand.Name.Ja);
        Assert.StartsWith("logo-satsuki.", testConfig.Brand.LogoImageName, StringComparison.Ordinal);
        Assert.Contains(testConfig.Brand.Theme, static x => (x.Role == "PrimaryColor") && (x.Color == "#B32B53"));
        Assert.All(testConfig.Brand.Theme, static x => Assert.True(ThemeRoles.IsRole(x.Role) && ThemeRoles.IsColor(x.Color)));
        Assert.Equal(["ja"], testConfig.Languages);
        Assert.Equal([PaymentMethod.QrCode], testConfig.PaymentMethods);
        Assert.False(testConfig.Features.SplitPayment);
        Assert.Equal((15, 20), (testConfig.Features.LastOrderNoticeMinutes, testConfig.Features.FinishSeconds));
        Assert.Equal(VisitOpening.Table, testConfig.Features.VisitOpening);
        Assert.True(StaffPins.Verify("5678", testConfig.StaffPin!.Iterations, testConfig.StaffPin.Salt, testConfig.StaffPin.Hash));
        Assert.False(StaffPins.Verify("1234", testConfig.StaffPin.Iterations, testConfig.StaffPin.Salt, testConfig.StaffPin.Hash));
    }

    // キッチン端末は、登録したときの持ち場を受け持つ。スタッフの PIN を使わないので、PIN のハッシュを受け取らない
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
        Assert.Null(config.StaffPin);
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

    // 範囲の外の電池の残りは受けない
    [Fact]
    public async Task HeartbeatRejectsInvalidBatteryLevel()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);

        // Act
        using var response = await device.PostAsync("/api/v1/devices/me/heartbeat", new DeviceHeartbeatRequest { AppVersion = "1.0.1", BatteryLevel = 1.5m });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 確かめられないトークン (署名の誤り) の 401 には、理由を付けない
    [Fact]
    public async Task InvalidTokenIsUnauthorizedWithoutReason()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);
        var parts = device.Client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        device.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", String.Join('.', parts[0], parts[1], new string(signature)));

        // Act
        using var response = await device.Client.GetAsync(new Uri("/api/v1/devices/me/config", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("error", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
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
}
