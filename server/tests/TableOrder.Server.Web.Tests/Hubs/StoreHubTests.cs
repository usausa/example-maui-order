namespace TableOrder.Server.Web.Hubs;

using TableOrder.Contract.Events;
using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;

public sealed class StoreHubTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public StoreHubTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 来店を開くと、ホールとそのテーブルの端末に届き、ほかのテーブルの端末には届かない
    [Fact]
    public async Task VisitOpenedReachesHallAndOwnTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[0]);
        using var other = await SignInAsync(store.TableCodes[1]);
        await using var hallHub = await TestHubConnection.ConnectAsync(factory, hall);
        await using var tableHub = await TestHubConnection.ConnectAsync(factory, table);
        await using var otherHub = await TestHubConnection.ConnectAsync(factory, other);

        // Act
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Assert
        var hallEvent = await hallHub.NextAsync();
        Assert.Equal(EventTypes.VisitOpened, hallEvent.Type);
        Assert.Equal(1, hallEvent.Seq);
        var tableEvent = await tableHub.NextAsync();
        Assert.Equal(EventTypes.VisitOpened, tableEvent.Type);
        Assert.Equal(visit.Id, TestHubConnection.Read<VisitResponse>(tableEvent).Id);
        var otherEvent = await otherHub.NextAsync();
        Assert.Equal(EventTypes.StoreUpdated, otherEvent.Type);
        Assert.True(TestHubConnection.Read<StoreResponse>(otherEvent).OrderingPaused);
    }

    // テーブルを移すと、元と移動先のテーブルの端末に届く
    [Fact]
    public async Task VisitMovedReachesBothTables()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var from = await SignInAsync(store.TableCodes[0]);
        using var to = await SignInAsync(store.TableCodes[1]);
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);
        await using var fromHub = await TestHubConnection.ConnectAsync(factory, from);
        await using var toHub = await TestHubConnection.ConnectAsync(factory, to);

        // Act
        using var moved = await hall.PostAsync($"/api/v1/visits/{visit.Id}/move", new VisitMoveRequest { ToTableId = store.TableIds[1], Version = visit.Version });
        moved.EnsureSuccessStatusCode();

        // Assert
        var fromEvent = await fromHub.NextAsync();
        var toEvent = await toHub.NextAsync();
        Assert.Equal(EventTypes.VisitMoved, fromEvent.Type);
        Assert.Equal(EventTypes.VisitMoved, toEvent.Type);
        var data = TestHubConnection.Read<VisitMovedEventData>(toEvent);
        Assert.Equal(store.TableIds[0], data.FromTableId);
        Assert.Equal(store.TableIds[1], data.Visit.TableId);
    }

    // 品切れはキッチン端末とテーブル端末に届き、受付機には届かない
    [Fact]
    public async Task StockUpdatedReachesMenuReaders()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        using var table = await SignInAsync(store.TableCodes[0]);
        using var reception = await SignInAsync(store.ReceptionCode);
        await using var kitchenHub = await TestHubConnection.ConnectAsync(factory, kitchen);
        await using var tableHub = await TestHubConnection.ConnectAsync(factory, table);
        await using var receptionHub = await TestHubConnection.ConnectAsync(factory, reception);
        var itemId = Guid.Parse("00000fa1-0000-0000-0000-000000000000");

        // Act
        using var soldOut = await hall.PutAsync($"/api/v1/stock/{itemId}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        soldOut.EnsureSuccessStatusCode();
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Assert
        var kitchenEvent = await kitchenHub.NextAsync();
        Assert.Equal(EventTypes.StockUpdated, kitchenEvent.Type);
        Assert.Equal(StockStatus.SoldOut, TestHubConnection.Read<StockUpdatedEventData>(kitchenEvent).Items.Single(x => x.TargetId == itemId).Status);
        Assert.Equal(EventTypes.StockUpdated, (await tableHub.NextAsync()).Type);
        Assert.Equal(EventTypes.StoreUpdated, (await receptionHub.NextAsync()).Type);
    }

    // 注文はホールとテーブル端末に、チケットはその持ち場のキッチン端末に届き、できあがりはテーブル端末の注文履歴に届く
    [Fact]
    public async Task OrderReachesHallTableAndKitchen()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[0]);
        using var kitchen = await SignInAsync(store.KitchenCode);
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);
        var menu = await TestMenu.LoadAsync(table);
        var salad = menu.Line(TestMenu.Salad);
        await using var hallHub = await TestHubConnection.ConnectAsync(factory, hall);
        await using var tableHub = await TestHubConnection.ConnectAsync(factory, table);
        await using var kitchenHub = await TestHubConnection.ConnectAsync(factory, kitchen);

        // Act / Assert: 注文
        using var ordered = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(salad));
        ordered.EnsureSuccessStatusCode();
        Assert.Equal(EventTypes.OrderCreated, (await hallHub.NextAsync()).Type);
        Assert.Equal(EventTypes.OrderCreated, (await tableHub.NextAsync()).Type);
        var created = await kitchenHub.NextAsync();
        Assert.Equal(EventTypes.TicketCreated, created.Type);
        var ticket = TestHubConnection.Read<KitchenTicketListResponseItem>(created);
        Assert.Equal(TestMenu.KitchenStation, ticket.StationId);

        // Act / Assert: できあがり
        using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/ready", new { });
        ready.EnsureSuccessStatusCode();
        var updated = await tableHub.NextAsync();
        Assert.Equal(EventTypes.OrderLinesUpdated, updated.Type);
        Assert.Equal(OrderLineStatus.Ready, TestHubConnection.Read<OrderLinesUpdatedEventData>(updated).Orders.Single().Lines.Single().Status);
        Assert.Equal(EventTypes.TicketUpdated, (await kitchenHub.NextAsync()).Type);
    }

    // トークンのない接続は受けない
    [Fact]
    public async Task ConnectionWithoutTokenIsRejected()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());

        // Act
        var exception = await Record.ExceptionAsync(async () => await TestHubConnection.ConnectAsync(factory, device));

        // Assert
        Assert.IsType<HttpRequestException>(exception);
        Assert.Equal(HttpStatusCode.Unauthorized, ((HttpRequestException)exception).StatusCode);
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }
}
