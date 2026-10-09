namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Client;
using TableOrder.Contract.Devices;
using TableOrder.Server.Core.Services;

// 管理画面の端末の管理 (サーバの中の Service を、管理画面と同じく選んだ店舗の文脈で呼ぶ)
public sealed class DeviceServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public DeviceServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private DeviceService Devices => factory.Services.GetRequiredService<DeviceService>();

    private DeviceEnrollmentService Enrollments => factory.Services.GetRequiredService<DeviceEnrollmentService>();

    // 出したペアリングコードで登録した端末は、コードで決めた置き場所になる。コードは 1 台だけ使える
    [Fact]
    public async Task IssuedPairingCodePlacesDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;

        // Act
        PairingCodeResult issued;
        using (factory.BeginStore(store))
        {
            issued = (await Enrollments.IssuePairingCodeAsync(DeviceKind.Table, store.TableIds[1], [], cancel)).Value!;
        }

        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(issued.Code);
        var config = await terminal.Device.GetConfigAsync(cancel);
        await using var other = TestTerminal.Create(factory);
        var again = await other.Device.PairAsync(new DevicePairRequest { PairingCode = issued.Code, PublicKey = DeviceCredentials.CreatePublicKey(await other.Context.Key.GetPublicKeyAsync()), DeviceName = "test" }, cancel);

        // Assert
        Assert.Equal(store.TableIds[1], config.Content!.Device!.TableId);
        Assert.Equal(ApiStatus.Rejected, again.Status);
        Assert.Equal("PAIRING_CODE_INVALID", again.ErrorCode);
    }

    // 種類に合わない置き場所と、ほかの店舗のテーブルではコードを出さない
    [Fact]
    public async Task IssueRejectsInvalidPlacement()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var other = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var scope = factory.BeginStore(store);

        // Act
        var hallWithTable = await Enrollments.IssuePairingCodeAsync(DeviceKind.Hall, store.TableIds[0], [], cancel);
        var otherTable = await Enrollments.IssuePairingCodeAsync(DeviceKind.Table, other.TableIds[0], [], cancel);
        var unknownStation = await Enrollments.IssuePairingCodeAsync(DeviceKind.Kitchen, null, [Guid.CreateVersion7()], cancel);

        // Assert
        Assert.Equal(ErrorCodes.ValidationError, hallWithTable.Error!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, otherTable.Error!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, unknownStation.Error!.ErrorCode);
    }

    // 置き場所と名前を替えると端末に知らせ、端末の設定は新しい置き場所になる。表示していた版が古ければ替えない
    [Fact]
    public async Task UpdateMovesDeviceAndNotifiesIt()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.PairAsync(store.TableCodes[0]);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);
        var version = await VersionOfAsync(store, paired.DeviceId);

        // Act / Assert: テーブル 2 に移して名前を替えると、その端末に知らせが届く
        using (factory.BeginStore(store))
        {
            Assert.Null(await Devices.UpdateAsync(paired.DeviceId, "T2", store.TableIds[1], [], version, cancel));
        }

        Assert.Equal(paired.DeviceId, Assert.IsType<DeviceUpdatedEvent>(await terminal.NextAsync()).DeviceId);
        var config = (await terminal.Device.GetConfigAsync(cancel)).Content!;
        Assert.Equal(store.TableIds[1], config.Device!.TableId);
        Assert.Equal("T2", config.Device.Name);

        // Act / Assert: 古い版では替えない
        using (factory.BeginStore(store))
        {
            Assert.Equal(ErrorCodes.VersionMismatch, (await Devices.UpdateAsync(paired.DeviceId, "T3", store.TableIds[2], [], version, cancel))!.ErrorCode);
        }
    }

    // 無効にすると端末に知らせ、次のトークンの要求は断られる
    [Fact]
    public async Task RevokeNotifiesAndDeniesNextToken()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.PairAsync(store.TableCodes[0]);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);
        var version = await VersionOfAsync(store, paired.DeviceId);

        // Act
        using (factory.BeginStore(store))
        {
            Assert.Null(await Devices.RevokeAsync(paired.DeviceId, version, cancel));
        }

        var e = Assert.IsType<DeviceUpdatedEvent>(await terminal.NextAsync());
        var authenticated = await terminal.Device.AuthenticateAsync(cancel);

        // Assert
        Assert.Equal(paired.DeviceId, e.DeviceId);
        Assert.Equal(ApiStatus.Unauthorized, authenticated.Status);
        Assert.Equal("DEVICE_REVOKED", authenticated.ErrorCode);
    }

    // 一覧は置き場所 (テーブルの名前、持ち場) と状態の報告を出し、ほかの店舗の端末は出さない
    [Fact]
    public async Task SummaryListShowsPlacementAndStatus()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var other = await factory.CreateStoreAsync();
        using var table = new TestDevice(factory.CreateClient());
        var tableDevice = await table.SignInAsync(store.TableCodes[0]);
        using var kitchen = new TestDevice(factory.CreateClient());
        var kitchenDevice = await kitchen.SignInAsync(store.KitchenCode);
        using var reported = await kitchen.PostAsync("/api/v1/devices/me/heartbeat", new DeviceHeartbeatRequest { AppVersion = "1.2.3", BatteryLevel = 0.5m, IsCharging = true });
        reported.EnsureSuccessStatusCode();
        using var outside = new TestDevice(factory.CreateClient());
        var outsideDevice = await outside.SignInAsync(other.TableCodes[0]);

        // Act
        List<DeviceSummaryResult> list;
        using (factory.BeginStore(store))
        {
            list = await Devices.GetSummaryListAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal("1", Assert.Single(list, x => x.Device.Id == tableDevice.DeviceId).Device.TableName);
        var kitchenItem = Assert.Single(list, x => x.Device.Id == kitchenDevice.DeviceId);
        Assert.Equal(3, kitchenItem.StationIds.Count);
        Assert.Equal(0.5m, kitchenItem.Device.BatteryLevel);
        Assert.Equal("1.2.3", kitchenItem.Device.AppVersion);
        Assert.DoesNotContain(list, x => x.Device.Id == outsideDevice.DeviceId);
    }

    // 期限を過ぎたペアリングコードは消し、そのコードでは登録できない
    [Fact]
    public async Task ExpiredPairingCodesAreDeleted()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        PairingCodeResult issued;
        using (factory.BeginStore(store))
        {
            issued = (await Enrollments.IssuePairingCodeAsync(DeviceKind.Hall, null, [], cancel)).Value!;
        }

        // Act
        var deleted = await Enrollments.DeleteExpiredAsync(issued.ExpiresAt.AddMinutes(1), cancel);
        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.Device.PairAsync(new DevicePairRequest { PairingCode = issued.Code, PublicKey = DeviceCredentials.CreatePublicKey(await terminal.Context.Key.GetPublicKeyAsync()), DeviceName = "test" }, cancel);

        // Assert
        Assert.True(deleted >= 1);
        Assert.Equal("PAIRING_CODE_INVALID", paired.ErrorCode);
    }

    private async Task<int> VersionOfAsync(TestStore store, Guid deviceId)
    {
        using var scope = factory.BeginStore(store);
        return (await Devices.GetSummaryListAsync(TestContext.Current.CancellationToken)).Single(x => x.Device.Id == deviceId).Device.Version;
    }
}
