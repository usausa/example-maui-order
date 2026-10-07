namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Menu;

public sealed class StockEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public StockEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 売り切れと残りの数のある品を返す
    [Fact]
    public async Task StockListsUnavailableItems()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoHallCode);

        // Act
        var stock = await device.GetAsync<StockResponse>("/api/v1/stock");

        // Assert
        Assert.Contains(stock.Items, static x => x.Status == StockStatus.SoldOut);
        Assert.Contains(stock.Items, static x => (x.Status == StockStatus.Limited) && (x.Remaining == 3));
    }

    // 品切れはテナント (店舗) ごと。検証用のテナントの店舗には品切れがない
    [Fact]
    public async Task StockIsSeparatedByTenant()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.TestTableCode);

        // Act
        var stock = await device.GetAsync<StockResponse>("/api/v1/stock");

        // Assert
        Assert.Empty(stock.Items);
    }
}
