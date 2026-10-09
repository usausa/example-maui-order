namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Calls;
using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Services;

// 店内の今 (管理画面と同じく、選んだ店舗の文脈で呼ぶ)
public sealed class FloorServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public FloorServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private FloorService Floor => factory.Services.GetRequiredService<FloorService>();

    // 終わっていない呼び出しに、テーブルと用件の名前を添える
    [Fact]
    public async Task CallsHaveReasonNames()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var visit = await factory.OpenVisitAsync(store, 0);
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" });
        created.EnsureSuccessStatusCode();

        // Act
        List<FloorCallResult> calls;
        using (factory.BeginStore(store))
        {
            calls = await Floor.GetCallsAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        var call = Assert.Single(calls);
        Assert.Equal("1", call.Call.TableName);
        Assert.Equal("お水", call.ReasonName.Ja);
    }

    // 品切れと残りの数に、今のメニューの商品とオプションの名前を添える
    [Fact]
    public async Task StockHasMenuNames()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        var menu = await hall.GetAsync<MenuResponse>("/api/v1/menu");
        using var soldOut = await hall.PutAsync($"/api/v1/stock/{TestMenu.Parfait}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        soldOut.EnsureSuccessStatusCode();
        using var limited = await hall.PutAsync($"/api/v1/stock/{TestMenu.CheeseSauce}", new StockUpdateRequest { TargetKind = StockTargetKind.Option, Status = StockStatus.Limited, Remaining = 5 });
        limited.EnsureSuccessStatusCode();

        // Act
        List<FloorStockResult> stock;
        using (factory.BeginStore(store))
        {
            stock = await Floor.GetStockAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        var item = Assert.Single(stock, static x => x.Stock.TargetId == TestMenu.Parfait);
        Assert.Equal(menu.Items.Single(static x => x.Id == TestMenu.Parfait).Name.Ja, item.Name!.Ja);
        var option = Assert.Single(stock, static x => x.Stock.TargetId == TestMenu.CheeseSauce);
        Assert.Equal(menu.OptionGroups.SelectMany(static x => x.Options).Single(static x => x.Id == TestMenu.CheeseSauce).Name.Ja, option.Name!.Ja);
        Assert.Equal(5, option.Stock.Remaining);
    }
}
