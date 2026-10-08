namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Menu;

public sealed class StockEndpointsTests : IClassFixture<ServerFactory>
{
    // メニューの品 (いちごパフェ、生ビール) とオプション (チーズのソース)
    private static readonly Guid ParfaitId = Guid.Parse("00000fa1-0000-0000-0000-000000000000");

    private static readonly Guid BeerId = Guid.Parse("00001772-0000-0000-0000-000000000000");

    private static readonly Guid CheeseOptionId = Guid.Parse("00001f4d-0000-0000-0000-000000000000");

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

    // キッチン端末が売り切れにした品と、残りの数を 0 にした品は SoldOut になり、Available に戻すと一覧から消える
    [Fact]
    public async Task KitchenUpdatesStock()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var kitchen = new TestDevice(factory.CreateClient());
        await kitchen.SignInAsync(store.KitchenCode);

        // Act / Assert: 売り切れと、残りの数
        using var soldOut = await kitchen.PutAsync($"/api/v1/stock/{ParfaitId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        using var limited = await kitchen.PutAsync($"/api/v1/stock/{CheeseOptionId}", new StockUpdateRequest { TargetKind = StockTargetKind.Option, Status = StockStatus.Limited, Remaining = 5 });
        using var zero = await kitchen.PutAsync($"/api/v1/stock/{BeerId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.Limited, Remaining = 0 });
        Assert.Equal(HttpStatusCode.NoContent, soldOut.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, limited.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, zero.StatusCode);
        var stock = await kitchen.GetAsync<StockResponse>("/api/v1/stock");
        Assert.Equal(StockStatus.SoldOut, stock.Items.Single(static x => x.TargetId == ParfaitId).Status);
        Assert.Equal(5, stock.Items.Single(static x => x.TargetId == CheeseOptionId).Remaining);
        Assert.Equal(StockStatus.SoldOut, stock.Items.Single(static x => x.TargetId == BeerId).Status);

        // Act / Assert: 売れるように戻す
        using var available = await kitchen.PutAsync($"/api/v1/stock/{ParfaitId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.Available });
        Assert.Equal(HttpStatusCode.NoContent, available.StatusCode);
        Assert.DoesNotContain((await kitchen.GetAsync<StockResponse>("/api/v1/stock")).Items, static x => x.TargetId == ParfaitId);
    }

    // メニューにない品と、種類の違う品は見つからない
    [Fact]
    public async Task UnknownTargetIsNotFound()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        using var unknown = await hall.PutAsync($"/api/v1/stock/{Guid.CreateVersion7()}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        using var wrongKind = await hall.PutAsync($"/api/v1/stock/{ParfaitId}", new StockUpdateRequest { TargetKind = StockTargetKind.Option, Status = StockStatus.SoldOut });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", await TestDevice.ReadErrorCodeAsync(unknown));
        Assert.Equal(HttpStatusCode.NotFound, wrongKind.StatusCode);
    }

    // ホール端末はすべて Available に戻せる
    [Fact]
    public async Task HallResetsStock()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var soldOut = await hall.PutAsync($"/api/v1/stock/{ParfaitId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        soldOut.EnsureSuccessStatusCode();

        // Act
        using var response = await hall.Client.PostAsync(new Uri("/api/v1/stock/reset", UriKind.Relative), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await hall.GetAsync<StockResponse>("/api/v1/stock")).Items);
    }

    // テーブル端末は品切れを変えられない
    [Fact]
    public async Task TableDeviceCannotUpdateStock()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);

        // Act
        using var response = await table.PutAsync($"/api/v1/stock/{ParfaitId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }
}
