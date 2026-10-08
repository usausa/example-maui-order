namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Visits;

public sealed class KitchenEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public KitchenEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 注文は持ち場ごとのチケットになり、持ち場を送るとその持ち場のチケットだけを返す
    [Fact]
    public async Task OrderCreatesTicketPerStation()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[1]);
        await OrderAsync(hall, visit, menu.Order(menu.Line(TestMenu.Salad, 2), menu.Line(TestMenu.Parfait)));

        // Act
        var all = await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets");
        var dessert = await kitchen.GetAsync<KitchenTicketListResponse>($"/api/v1/kitchen/tickets?stationId={TestMenu.DessertStation}");

        // Assert
        Assert.Equal(2, all.Items.Count);
        var ticket = Assert.Single(dessert.Items);
        Assert.Equal("2", ticket.TableName);
        Assert.Equal(1, ticket.OrderNo);
        Assert.Equal(KitchenTicketStatus.Open, ticket.Status);
        Assert.Equal(OrderLineStatus.Ordered, Assert.Single(ticket.Lines).Status);
    }

    // 作り始めとできあがりで明細が進み、下げたチケットは戻すと作っている途中に戻る
    [Fact]
    public async Task TicketGoesThroughCookingAndBump()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var salad = menu.Line(TestMenu.Salad);
        var hamburg = menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.Demiglace);
        await OrderAsync(hall, visit, menu.Order(salad, hamburg));
        var ticket = Assert.Single((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);

        // Act / Assert: 作り始めとできあがり
        using var started = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/start", new { });
        Assert.Equal(OrderLineStatus.Cooking, (await TestDevice.ReadAsync<KitchenTicketListResponseItem>(started)).Lines.Single(x => x.LineId == salad.Id).Status);
        using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/ready", new { });
        Assert.Equal(OrderLineStatus.Ready, (await TestDevice.ReadAsync<KitchenTicketListResponseItem>(ready)).Lines.Single(x => x.LineId == salad.Id).Status);

        // Act / Assert: 下げると残りの明細もできあがりになり、開いている一覧から外れる
        using var bumped = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/bump", new { });
        var done = await TestDevice.ReadAsync<KitchenTicketListResponseItem>(bumped);
        Assert.Equal(KitchenTicketStatus.Done, done.Status);
        Assert.All(done.Lines, static x => Assert.Equal(OrderLineStatus.Ready, x.Status));
        Assert.Empty((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        Assert.Equal([ticket.Id], (await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets?status=Done")).Items.Select(static x => x.Id));

        // Act / Assert: 戻すと開き、まだ出していない明細は作っている途中に戻る
        using var recalled = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/recall", new { });
        var reopened = await TestDevice.ReadAsync<KitchenTicketListResponseItem>(recalled);
        Assert.Equal(KitchenTicketStatus.Open, reopened.Status);
        Assert.All(reopened.Lines, static x => Assert.Equal(OrderLineStatus.Cooking, x.Status));
        var history = await hall.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.All(history.Items[0].Lines, static x => Assert.Equal(OrderLineStatus.Cooking, x.Status));
    }

    // できあがった明細は作り始めに戻せない
    [Fact]
    public async Task StartAfterReadyIsInvalid()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var salad = menu.Line(TestMenu.Salad);
        await OrderAsync(hall, visit, menu.Order(salad));
        var ticket = Assert.Single((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/ready", new { });
        ready.EnsureSuccessStatusCode();

        // Act
        using var response = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/start", new { });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("LINE_STATUS_INVALID", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 受け持たない持ち場は読めず、キッチン端末でなければチケットを読めない
    [Fact]
    public async Task TicketsAreScopedToStations()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var kitchen = await SignInAsync(store.KitchenCode);
        using var hall = await SignInAsync(store.HallCode);

        // Act
        using var otherStation = await kitchen.Client.GetAsync(new Uri($"/api/v1/kitchen/tickets?stationId={Guid.CreateVersion7()}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var fromHall = await hall.Client.GetAsync(new Uri("/api/v1/kitchen/tickets", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, otherStation.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(otherStation));
        Assert.Equal(HttpStatusCode.Forbidden, fromHall.StatusCode);
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    private static async Task<VisitResponse> OpenAsync(TestDevice device, Guid tableId)
    {
        using var response = await device.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = tableId, Adults = 2 });
        return await TestDevice.ReadAsync<VisitResponse>(response);
    }

    private static async Task OrderAsync(TestDevice device, VisitResponse visit, OrderCreateRequest request)
    {
        using var response = await device.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);
        response.EnsureSuccessStatusCode();
    }
}
