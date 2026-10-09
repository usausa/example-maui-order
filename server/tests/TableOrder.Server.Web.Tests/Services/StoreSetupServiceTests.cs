namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Services;

public sealed class StoreSetupServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public StoreSetupServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private StoreSetupService Service => factory.Services.GetRequiredService<StoreSetupService>();

    private static MenuSeed SampleMenu => new("sample", """{"menuVersion":"sample","categories":[],"items":[],"optionGroups":[],"tags":[],"rules":[],"allergens":[],"stations":[]}""");

    // 選んだ店舗から設定 (言語、支払方法、機能、呼び出しの用件、メニュー) を写して店舗を足し、端末を登録して使える
    [Fact]
    public async Task AddCopiesSettingsFromSourceStore()
    {
        // Arrange
        var source = await factory.CreateStoreAsync();
        using var scope = factory.BeginTenant(source.TenantId);

        // Act
        var result = await Service.AddAsync(Basics("002"), "5678", source.StoreId, SampleMenu, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var store = result.Value;
        var sourceStore = (await factory.Services.GetRequiredService<StoreAccessor>().QueryAsync(source.TenantId, source.StoreId, TestContext.Current.CancellationToken))!;
        Assert.Equal(sourceStore.PaymentMethods, store.PaymentMethods);
        Assert.Equal(sourceStore.Features, store.Features);
        Assert.Equal("Asia/Tokyo", store.TimeZone);
        var menus = factory.Services.GetRequiredService<MenuAccessor>();
        Assert.Equal(
            (await menus.QueryCurrentAsync(source.TenantId, source.StoreId, TestContext.Current.CancellationToken))!.MenuVersion,
            (await menus.QueryCurrentAsync(source.TenantId, store.Id, TestContext.Current.CancellationToken))!.MenuVersion);
        var reasons = await factory.Services.GetRequiredService<StoreAccessor>().QueryCallReasonListAsync(source.TenantId, store.Id, TestContext.Current.CancellationToken);
        Assert.Equal(["Staff", "Water"], reasons.Select(static x => x.Code));
    }

    // 写す店舗がなければ、既定の設定と渡したメニュー (サンプルのメニュー) にする
    [Fact]
    public async Task AddWithoutSourceUsesFallbackMenu()
    {
        // Arrange
        var (tenantId, _) = await factory.CreateTenantAsync();
        using var scope = factory.BeginTenant(tenantId);

        // Act
        var result = await Service.AddAsync(Basics("002"), "5678", null, SampleMenu, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("""["ja","en"]""", result.Value.Languages);
        Assert.Equal("sample", (await factory.Services.GetRequiredService<MenuAccessor>().QueryCurrentAsync(tenantId, result.Value.Id, TestContext.Current.CancellationToken))!.MenuVersion);
    }

    // 店舗コードはテナントの中で一意で、基本の値は決まりに合わせる
    [Fact]
    public async Task AddRejectsInvalidBasics()
    {
        // Arrange
        var source = await factory.CreateStoreAsync();
        using var scope = factory.BeginTenant(source.TenantId);

        // Act
        var duplicate = await Service.AddAsync(Basics("001"), "5678", null, SampleMenu, TestContext.Current.CancellationToken);
        var time = await Service.AddAsync(Basics("003") with { OpenTime = "25:00" }, "5678", null, SampleMenu, TestContext.Current.CancellationToken);
        var zone = await Service.AddAsync(Basics("004") with { TimeZone = "Mars/Base" }, "5678", null, SampleMenu, TestContext.Current.CancellationToken);
        var pin = await Service.AddAsync(Basics("005"), "12", null, SampleMenu, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("code", duplicate.Error!.Errors!.Keys);
        Assert.Contains("openTime", time.Error!.Errors!.Keys);
        Assert.Contains("timeZone", zone.Error!.Errors!.Keys);
        Assert.Contains("staffPin", pin.Error!.Errors!.Keys);
    }

    // 基本を替えると設定の版が上がり、端末の店舗の応答も替わる
    [Fact]
    public async Task UpdateChangesBasicsAndSettingsVersion()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var stores = factory.Services.GetRequiredService<StoreAccessor>();
        var before = (await stores.QueryAsync(store.TenantId, store.StoreId, TestContext.Current.CancellationToken))!;
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        ServiceError? error;
        using (factory.BeginTenant(store.TenantId))
        {
            error = await Service.UpdateAsync(store.StoreId, Basics("001") with { LastOrderTime = "21:30", Name = new LocalizedText { Ja = "新しい店" } }, before.Version, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Null(error);
        var after = (await stores.QueryAsync(store.TenantId, store.StoreId, TestContext.Current.CancellationToken))!;
        Assert.Equal(before.SettingsVersion + 1, after.SettingsVersion);
        var response = await hall.GetAsync<StoreResponse>("/api/v1/store");
        Assert.Equal("21:30", response.LastOrderTime);
        Assert.Equal("新しい店", response.Name.Ja);
    }

    // 使っている端末か開いている来店があれば使わなくできず、使わなくした店舗には端末を登録しない
    [Fact]
    public async Task DeactivateRequiresNoDevicesAndVisits()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var stores = factory.Services.GetRequiredService<StoreAccessor>();
        using var hall = new TestDevice(factory.CreateClient());
        var paired = await hall.SignInAsync(store.HallCode);
        var version = (await stores.QueryAsync(store.TenantId, store.StoreId, TestContext.Current.CancellationToken))!.Version;

        // Act / Assert: 使っている端末がある
        using (factory.BeginTenant(store.TenantId))
        {
            Assert.Contains("isActive", (await Service.SetActiveAsync(store.StoreId, false, version, TestContext.Current.CancellationToken))!.Errors!.Keys);
        }

        // Act / Assert: 端末を無効にすると使わなくでき、残ったコードでも登録しない
        await factory.RevokeDeviceAsync(paired.DeviceId);
        using (factory.BeginTenant(store.TenantId))
        {
            Assert.Null(await Service.SetActiveAsync(store.StoreId, false, version, TestContext.Current.CancellationToken));
        }

        using var table = new TestDevice(factory.CreateClient());
        using var pair = await table.PairAsync(store.TableCodes[0]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pair.StatusCode);
    }

    private static StoreBasics Basics(string code) =>
        new(code, new LocalizedText { Ja = "店", En = "Store" }, "Asia/Tokyo", "10:00", "23:00", "22:30", TaxRounding.Floor, 9, 20);
}
