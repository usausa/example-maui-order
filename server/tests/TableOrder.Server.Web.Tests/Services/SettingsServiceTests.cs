namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Client;
using TableOrder.Contract.Devices;
using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Services;

// 管理画面のチェーンと店舗の設定 (サーバの中の SettingsService を、管理画面と同じく選んだ店舗の文脈で呼ぶ)
public sealed class SettingsServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public SettingsServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private SettingsService Settings => factory.Services.GetRequiredService<SettingsService>();

    // チェーンの設定を替えると、店舗の設定の版が上がって端末に知らせ、端末の設定は新しい名前と色になる
    [Fact]
    public async Task BrandUpdateRaisesSettingsVersionAndNotifies()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        var before = (await terminal.Device.GetConfigAsync(cancel)).Content!;
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);

        // Act
        ServiceResult<BrandUpdateResult> result;
        using (factory.BeginStore(store))
        {
            var brand = (await Settings.GetBrandAsync(cancel))!;
            result = await Settings.UpdateBrandAsync(brand with { Name = new LocalizedText { Ja = "新しいチェーン", En = "New chain" }, Theme = new Dictionary<string, string> { ["PrimaryColor"] = "#1E5FA8" } }, cancel);
        }

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Value.UnnotifiedStores);
        var updated = Assert.IsType<StoreUpdatedEvent>(await terminal.NextAsync());
        Assert.Equal(before.SettingsVersion + 1, updated.Store.SettingsVersion);
        var after = (await terminal.Device.GetConfigAsync(cancel)).Content!;
        Assert.Equal("新しいチェーン", after.Brand.Name.Ja);
        Assert.Equal("#1E5FA8", Assert.Single(after.Brand.Theme, static x => x.Role == "PrimaryColor").Color);
        Assert.Equal(updated.Store.SettingsVersion, after.SettingsVersion);
    }

    // 知らない色の役割、形の違う色、置いていないロゴ、古い版のチェーンの設定は受けない
    [Fact]
    public async Task BrandUpdateRejectsInvalidSettings()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var scope = factory.BeginStore(store);
        var brand = (await Settings.GetBrandAsync(cancel))!;

        // Act
        var unknownRole = await Settings.UpdateBrandAsync(brand with { Theme = new Dictionary<string, string> { ["SystemAccentColor"] = "#000000" } }, cancel);
        var badColor = await Settings.UpdateBrandAsync(brand with { Theme = new Dictionary<string, string> { ["PrimaryColor"] = "blue" } }, cancel);
        var missingLogo = await Settings.UpdateBrandAsync(brand with { LogoImageName = "missing.12345678.png" }, cancel);
        var stale = await Settings.UpdateBrandAsync(brand with { Version = brand.Version - 1 }, cancel);

        // Assert
        Assert.Equal(ErrorCodes.ValidationError, unknownRole.Error?.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, badColor.Error?.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, missingLogo.Error?.ErrorCode);
        Assert.Equal(ErrorCodes.VersionMismatch, stale.Error?.ErrorCode);
    }

    // 店舗の設定を替えると端末に知らせ、端末の設定は新しい機能・来店の開き方・時間帯の猶予・言語・支払方法・電子レシート・呼び出しの用件・PIN になる
    [Fact]
    public async Task StoreSettingsUpdateChangesConfig()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);

        // Act
        ServiceError? error;
        using (factory.BeginStore(store))
        {
            var settings = (await Settings.GetStoreSettingsAsync(cancel))!;
            error = await Settings.UpdateStoreSettingsAsync(
                settings with
                {
                    Features = new DeviceConfigResponseFeatures { RegisterCheckout = false, SplitPayment = false, LastOrderNoticeMinutes = 0, FinishSeconds = 10, VisitOpening = VisitOpening.Reception, KitchenAlertMinutes = 0, DaypartGraceMinutes = 5 },
                    Languages = ["ja", "en"],
                    PaymentMethods = [PaymentMethod.QrCode],
                    ElectronicReceipt = false,
                    CallReasons = settings.CallReasons.Select(static x => x with { IsActive = x.Code == "Water" }).ToList()
                },
                "9876",
                cancel);
        }

        // Assert
        Assert.Null(error);
        Assert.IsType<StoreUpdatedEvent>(await terminal.NextAsync());
        var config = (await terminal.Device.GetConfigAsync(cancel)).Content!;
        Assert.Equal((false, false, 0, 10), (config.Features.RegisterCheckout, config.Features.SplitPayment, config.Features.LastOrderNoticeMinutes, config.Features.FinishSeconds));
        Assert.Equal(VisitOpening.Reception, config.Features.VisitOpening);
        Assert.Equal(0, config.Features.KitchenAlertMinutes);
        Assert.Equal(5, config.Features.DaypartGraceMinutes);
        Assert.Equal(["ja", "en"], config.Languages);
        Assert.Equal([PaymentMethod.QrCode], config.PaymentMethods);
        Assert.False(config.ElectronicReceipt);
        Assert.Equal("Water", Assert.Single(config.CallReasons).Code);
        Assert.True(StaffPins.Verify("9876", config.StaffPin!.Iterations, config.StaffPin.Salt, config.StaffPin.Hash));
    }

    // 店舗の設定を読んだあとにホールが注文を止めても、読んだ版のまま保存できる (一時停止は店舗の編集の版を上げない)
    [Fact]
    public async Task StoreSettingsSaveAfterOrderingPause()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        StoreSettings settings;
        using (factory.BeginStore(store))
        {
            settings = (await Settings.GetStoreSettingsAsync(cancel))!;
        }

        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Act
        ServiceError? error;
        using (factory.BeginStore(store))
        {
            error = await Settings.UpdateStoreSettingsAsync(settings with { Languages = ["ja"] }, null, cancel);
        }

        // Assert
        Assert.Null(error);
    }

    // PIN を送らなければ替えない。払う手段がなくなる設定、形の違う PIN と言語、ない来店の開き方、範囲の外のキッチンの遅れの時間と時間帯の猶予、古い版は受けない
    [Fact]
    public async Task StoreSettingsKeepPinAndRejectInvalidSettings()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        using var scope = factory.BeginStore(store);
        var settings = (await Settings.GetStoreSettingsAsync(cancel))!;

        // Act
        var kept = await Settings.UpdateStoreSettingsAsync(settings, null, cancel);
        var noWayToPay = await Settings.UpdateStoreSettingsAsync(settings with { Features = new DeviceConfigResponseFeatures { RegisterCheckout = false }, PaymentMethods = [], Version = settings.Version + 1 }, null, cancel);
        var badPin = await Settings.UpdateStoreSettingsAsync(settings with { Version = settings.Version + 1 }, "12a4", cancel);
        var noLanguage = await Settings.UpdateStoreSettingsAsync(settings with { Languages = [], Version = settings.Version + 1 }, null, cancel);
        var badOpening = await Settings.UpdateStoreSettingsAsync(settings with { Features = new DeviceConfigResponseFeatures { VisitOpening = (VisitOpening)99 }, Version = settings.Version + 1 }, null, cancel);
        var badAlert = await Settings.UpdateStoreSettingsAsync(settings with { Features = new DeviceConfigResponseFeatures { KitchenAlertMinutes = 121 }, Version = settings.Version + 1 }, null, cancel);
        var badGrace = await Settings.UpdateStoreSettingsAsync(settings with { Features = new DeviceConfigResponseFeatures { DaypartGraceMinutes = 11 }, Version = settings.Version + 1 }, null, cancel);
        var stale = await Settings.UpdateStoreSettingsAsync(settings, null, cancel);

        // Assert
        Assert.Null(kept);
        var config = (await terminal.Device.GetConfigAsync(cancel)).Content!;
        Assert.True(StaffPins.Verify(ServerFactory.StaffPin, config.StaffPin!.Iterations, config.StaffPin.Salt, config.StaffPin.Hash));
        Assert.Equal(ErrorCodes.ValidationError, noWayToPay!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, badPin!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, noLanguage!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, badOpening!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, badAlert!.ErrorCode);
        Assert.Equal(ErrorCodes.ValidationError, badGrace!.ErrorCode);
        Assert.Equal(ErrorCodes.VersionMismatch, stale!.ErrorCode);
    }
}
