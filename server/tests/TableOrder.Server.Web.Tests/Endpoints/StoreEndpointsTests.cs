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
}
