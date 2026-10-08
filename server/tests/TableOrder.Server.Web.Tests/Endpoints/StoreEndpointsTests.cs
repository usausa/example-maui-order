namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Stores;

public sealed class StoreEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public StoreEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 店舗はトークンのテナントと店舗で決まる (同じ店舗コードでも、ほかのテナントの店舗は返さない)
    [Fact]
    public async Task StoreIsTheStoreOfTheToken()
    {
        // Arrange
        using var demo = new TestDevice(factory.CreateClient());
        await demo.SignInAsync(SampleData.DemoTableCode);
        using var test = new TestDevice(factory.CreateClient());
        await test.SignInAsync(SampleData.TestTableCode);

        // Act
        var demoStore = await demo.GetAsync<StoreResponse>("/api/v1/store");
        var testStore = await test.GetAsync<StoreResponse>("/api/v1/store");

        // Assert
        Assert.Equal(SampleData.DemoStoreId, demoStore.Id);
        Assert.Equal("駅前店", demoStore.Name.Ja);
        Assert.Equal(SampleData.TestStoreId, testStore.Id);
        Assert.Equal("本店", testStore.Name.Ja);
        Assert.Equal(demoStore.Code, testStore.Code);
    }

    // 受付機も店舗を読める
    [Fact]
    public async Task ReceptionCanReadStore()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoReceptionCode);

        // Act
        var store = await device.GetAsync<StoreResponse>("/api/v1/store");

        // Assert
        Assert.Equal("Asia/Tokyo", store.TimeZone);
    }

    // ホール端末が注文を止めると、店舗に一時停止と文言が出て、再開すると文言が消える
    [Fact]
    public async Task HallPausesAndResumesOrdering()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);

        // Act / Assert: 一時停止
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true, Message = new LocalizedText { Ja = "ただいま混み合っています" } });
        Assert.Equal(HttpStatusCode.NoContent, paused.StatusCode);
        var pausedStore = await table.GetAsync<StoreResponse>("/api/v1/store");
        Assert.True(pausedStore.OrderingPaused);
        Assert.Equal("ただいま混み合っています", pausedStore.PausedMessage!.Ja);

        // Act / Assert: 再開 (送った文言は使わない)
        using var resumed = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = false, Message = new LocalizedText { Ja = "残らない文言" } });
        Assert.Equal(HttpStatusCode.NoContent, resumed.StatusCode);
        var resumedStore = await table.GetAsync<StoreResponse>("/api/v1/store");
        Assert.False(resumedStore.OrderingPaused);
        Assert.Null(resumedStore.PausedMessage);
    }

    // テーブル端末は注文を止められない
    [Fact]
    public async Task TableDeviceCannotPauseOrdering()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);

        // Act
        using var response = await table.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }
}
