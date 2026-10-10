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
        await terminal.PairAsync(issued.Code, DeviceKind.Table);
        var config = await terminal.Device.GetConfigAsync(cancel);
        await using var other = TestTerminal.Create(factory);
        var again = await other.Device.PairAsync(await other.CreatePairRequestAsync(issued.Code, null, DeviceKind.Table), cancel);

        // Assert
        Assert.Equal(store.TableIds[1], config.Content!.Device!.TableId);
        Assert.Equal(ApiStatus.Rejected, again.Status);
        Assert.Equal("PAIRING_CODE_INVALID", again.ErrorCode);
    }

    // 種類の違うアプリの登録は、コードを使わずに断る (同じコードで正しい種類のアプリは登録できる)
    [Fact]
    public async Task KindMismatchKeepsPairingCode()
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
        await using var table = TestTerminal.Create(factory);
        var mismatched = await table.Device.PairAsync(await table.CreatePairRequestAsync(issued.Code, null, DeviceKind.Table), cancel);
        await using var hall = TestTerminal.Create(factory);
        var paired = await hall.PairAsync(issued.Code, DeviceKind.Hall);

        // Assert
        Assert.Equal(ApiStatus.Rejected, mismatched.Status);
        Assert.Equal("DEVICE_KIND_MISMATCH", mismatched.ErrorCode);
        Assert.Equal(DeviceKind.Hall, paired.Kind);
        using (factory.BeginStore(store))
        {
            Assert.Equal(paired.DeviceId, Assert.Single(await Devices.GetSummaryListAsync(cancel), static x => x.Device.Kind == DeviceKind.Hall).Device.Id);
        }
    }

    // コードを出したあとに使わなくしたテーブルには置かず、置き場所の割り当てを待つ端末として登録する
    [Fact]
    public async Task PairingCodeForInactiveTableLeavesPlacementEmpty()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        PairingCodeResult issued;
        using (factory.BeginStore(store))
        {
            issued = (await Enrollments.IssuePairingCodeAsync(DeviceKind.Table, store.TableIds[2], [], cancel)).Value!;
            var tables = factory.Services.GetRequiredService<TableSetupService>();
            var version = (await tables.GetListAsync(cancel)).Single(x => x.Id == store.TableIds[2]).Version;
            Assert.Null(await tables.SetActiveAsync(store.TableIds[2], false, version, cancel));
        }

        // Act
        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.PairAsync(issued.Code, DeviceKind.Table);
        var config = await terminal.Device.GetConfigAsync(cancel);

        // Assert
        Assert.Null(config.Content!.Device!.TableId);
        using (factory.BeginStore(store))
        {
            Assert.True(Assert.Single(await Devices.GetSummaryListAsync(cancel), x => x.Device.Id == paired.DeviceId).IsWaitingPlacement);
        }
    }

    // 同じ鍵で登録し直した端末が新しい登録でトークンを受け取ると、前の登録を無効にして前の店舗に知らせる
    // (前の登録がテーブルを使ったまま残らない)。新しい登録を使うまでは、前の登録を残す
    [Fact]
    public async Task RepairingRevokesPreviousRegistration()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        var previous = await terminal.PairAsync(store.TableCodes[0]);
        await using var hall = TestTerminal.Create(factory);
        await hall.PairAsync(store.HallCode);
        Assert.True((await hall.Events.ConnectAsync(cancel)).IsSuccess);

        // Act / Assert: 登録し直しただけでは、前の登録を残す (応答が届かなければ、端末は前の登録を使い続ける)
        var current = await terminal.PairAsync(store.TableCodes[1]);
        Assert.True(await IsActiveAsync(store, previous.DeviceId));

        // Act / Assert: 新しい登録でトークンを受け取ると、前の登録を無効にして知らせる
        Assert.True((await terminal.Device.AuthenticateAsync(cancel)).IsSuccess);
        Assert.Equal(previous.DeviceId, Assert.IsType<DeviceUpdatedEvent>(await hall.NextAsync()).DeviceId);
        Assert.False(await IsActiveAsync(store, previous.DeviceId));
        Assert.True(await IsActiveAsync(store, current.DeviceId));
    }

    // ほかの端末の公開鍵を送って作った登録は、その端末の登録を無効にしない (鍵を持たないので、新しい登録でトークンを受け取れない)
    [Fact]
    public async Task PairingWithOtherPublicKeyKeepsDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var device = new TestDevice(factory.CreateClient());
        var paired = await device.SignInAsync(store.TableCodes[0]);
        using var other = factory.CreateClient();

        // Act
        using var copied = await other.PostAsJsonAsync("/api/v1/devices/pair", new DevicePairRequest { PairingCode = store.TableCodes[1], Kind = DeviceKind.Table, PublicKey = device.PublicKey, DeviceName = "copy" }, TestDevice.JsonOptions, TestContext.Current.CancellationToken);
        using var token = await device.RequestTokenAsync(device.CreateAssertion());

        // Assert
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.True(await IsActiveAsync(store, paired.DeviceId));
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

    // 登録トークンは決めた台数まで登録でき、登録した端末は置き場所の割り当てを待つ (一覧の先頭に出す)
    [Fact]
    public async Task EnrollmentTokenPairsUpToMaxUses()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var placed = TestTerminal.Create(factory);
        var placedDevice = await placed.PairAsync(store.TableCodes[0]);
        EnrollmentTokenResult issued;
        using (factory.BeginStore(store))
        {
            issued = (await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Table, 2, 7, cancel)).Value!;
        }

        // Act
        await using var first = TestTerminal.Create(factory);
        var firstDevice = await first.PairByTokenAsync(issued.Token, DeviceKind.Table);
        await using var second = TestTerminal.Create(factory);
        var secondDevice = await second.PairByTokenAsync(issued.Token, DeviceKind.Table);
        await using var third = TestTerminal.Create(factory);
        var over = await third.Device.PairAsync(await third.CreatePairRequestAsync(null, issued.Token, DeviceKind.Table), cancel);
        var config = await first.Device.GetConfigAsync(cancel);

        // Assert
        Assert.Equal("PAIRING_CODE_INVALID", over.ErrorCode);
        Assert.Null(config.Content!.Device!.TableId);
        using (factory.BeginStore(store))
        {
            var token = Assert.Single(await Enrollments.GetTokenListAsync(cancel));
            Assert.Equal(issued.Id, token.Id);
            Assert.Equal(2, token.UsedCount);
            var devices = await Devices.GetSummaryListAsync(cancel);
            Assert.Equal(new[] { firstDevice.DeviceId, secondDevice.DeviceId }.Order(), devices.Take(2).Select(static x => x.Device.Id).Order());
            Assert.All(devices.Take(2), static x => Assert.True(x.IsWaitingPlacement));
            Assert.False(Assert.Single(devices, x => x.Device.Id == placedDevice.DeviceId).IsWaitingPlacement);
        }
    }

    // 登録トークンの台数は 1~1000、期限は 1~30 日
    [Theory]
    [InlineData(1, 1, null)]
    [InlineData(1000, 30, null)]
    [InlineData(0, 7, "maxUses")]
    [InlineData(1001, 7, "maxUses")]
    [InlineData(10, 0, "days")]
    [InlineData(10, 31, "days")]
    public async Task IssueEnrollmentTokenChecksRange(int maxUses, int days, string? key)
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var scope = factory.BeginStore(store);

        // Act
        var result = await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Kitchen, maxUses, days, TestContext.Current.CancellationToken);

        // Assert
        if (key is null)
        {
            Assert.True(result.Succeeded);
            Assert.Equal(maxUses, Assert.Single(await Enrollments.GetTokenListAsync(TestContext.Current.CancellationToken)).MaxUses);
        }
        else
        {
            Assert.Contains(key, result.Error!.Errors!.Keys);
            Assert.Empty(await Enrollments.GetTokenListAsync(TestContext.Current.CancellationToken));
        }
    }

    // 取り消した登録トークンでは登録できない (取り消したものの取り消し直しは成功にする)
    [Fact]
    public async Task RevokedEnrollmentTokenRejectsPairing()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        EnrollmentTokenResult issued;
        ServiceError? revoked;
        ServiceError? again;

        // Act
        using (factory.BeginStore(store))
        {
            issued = (await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Hall, 5, 1, cancel)).Value!;
            revoked = await Enrollments.RevokeTokenAsync(issued.Id, cancel);
            again = await Enrollments.RevokeTokenAsync(issued.Id, cancel);
        }

        await using var terminal = TestTerminal.Create(factory);
        var paired = await terminal.Device.PairAsync(await terminal.CreatePairRequestAsync(null, issued.Token, DeviceKind.Hall), cancel);

        // Assert
        Assert.Null(revoked);
        Assert.Null(again);
        Assert.Equal("PAIRING_CODE_INVALID", paired.ErrorCode);
        using (factory.BeginStore(store))
        {
            Assert.NotNull(Assert.Single(await Enrollments.GetTokenListAsync(cancel)).RevokedAt);
        }
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

    // 無効にすると端末の通知の接続を切り、端末はつなぎ直しを断られて、トークンの要求で無効にされたことを知る
    [Fact]
    public async Task RevokeClosesConnectionAndDeniesDevice()
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

        var denied = await terminal.NextDeniedAsync();

        // Assert
        Assert.Equal(paired.DeviceId, denied.DeviceId);
        Assert.Equal(DeviceDenial.Revoked, denied.Reason);
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
        var paired = await terminal.Device.PairAsync(await terminal.CreatePairRequestAsync(issued.Code, null, DeviceKind.Hall), cancel);

        // Assert
        Assert.True(deleted >= 1);
        Assert.Equal("PAIRING_CODE_INVALID", paired.ErrorCode);
    }

    // 登録トークンは、期限を過ぎるか取り消してから 1 日は残し (一覧に出す)、そのあとに消す
    [Fact]
    public async Task EnrollmentTokensAreDeletedOneDayAfterExpiryOrRevocation()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var scope = factory.BeginStore(store);
        var expiring = (await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Table, 1, 1, cancel)).Value!;
        var revoked = (await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Table, 1, 30, cancel)).Value!;
        var kept = (await Enrollments.IssueEnrollmentTokenAsync(DeviceKind.Table, 1, 30, cancel)).Value!;
        Assert.Null(await Enrollments.RevokeTokenAsync(revoked.Id, cancel));
        var now = DateTimeOffset.UtcNow;

        // Act / Assert: 取り消してから 1 日のうちは残す
        await Enrollments.DeleteExpiredAsync(now.AddHours(23), cancel);
        Assert.Equal(3, (await Enrollments.GetTokenListAsync(cancel)).Count);

        // Act / Assert: 取り消してから 1 日を過ぎたら消す
        await Enrollments.DeleteExpiredAsync(now.AddDays(1).AddMinutes(1), cancel);
        Assert.Equal(new[] { expiring.Id, kept.Id }.Order(), (await Enrollments.GetTokenListAsync(cancel)).Select(static x => x.Id).Order());

        // Act / Assert: 期限を過ぎてから 1 日を過ぎたら消す
        await Enrollments.DeleteExpiredAsync(expiring.ExpiresAt.AddDays(1).AddMinutes(1), cancel);
        Assert.Equal(kept.Id, Assert.Single(await Enrollments.GetTokenListAsync(cancel)).Id);
    }

    private async Task<bool> IsActiveAsync(TestStore store, Guid deviceId)
    {
        using var scope = factory.BeginStore(store);
        return (await Devices.GetSummaryListAsync(TestContext.Current.CancellationToken)).Single(x => x.Device.Id == deviceId).Device.IsActive;
    }

    private async Task<int> VersionOfAsync(TestStore store, Guid deviceId)
    {
        using var scope = factory.BeginStore(store);
        return (await Devices.GetSummaryListAsync(TestContext.Current.CancellationToken)).Single(x => x.Device.Id == deviceId).Device.Version;
    }
}
