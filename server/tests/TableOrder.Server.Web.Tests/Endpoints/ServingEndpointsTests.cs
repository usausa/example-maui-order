namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Serving;
using TableOrder.Contract.Visits;

public sealed class ServingEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public ServingEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // できあがった明細はテーブルごとに提供の一覧に出て、提供すると一覧から消える
    [Fact]
    public async Task HallServesReadyLines()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[2]);
        var salad = menu.Line(TestMenu.Salad);
        await OrderAsync(hall, visit, menu.Order(salad));
        var ticket = Assert.Single((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/ready", new { });
        ready.EnsureSuccessStatusCode();

        // Act / Assert: 提供の一覧
        var serving = await hall.GetAsync<ServingListResponse>("/api/v1/serving");
        var table = Assert.Single(serving.Items);
        Assert.Equal("3", table.TableName);
        Assert.Equal(salad.Id, Assert.Single(table.Lines).LineId);

        // Act / Assert: 提供する
        using var served = await hall.PostAsync("/api/v1/serving/serve", new ServeRequest { LineIds = [salad.Id], StaffId = "S01" });
        Assert.Equal(HttpStatusCode.NoContent, served.StatusCode);
        Assert.Empty((await hall.GetAsync<ServingListResponse>("/api/v1/serving")).Items);
        var history = await hall.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.Equal(OrderLineStatus.Served, Assert.Single(history.Items[0].Lines).Status);
    }

    // できあがりの前の品も出せるが、食後まで止めている品は出せない
    [Fact]
    public async Task ServeChecksLineStatus()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var salad = menu.Line(TestMenu.Salad);
        var parfait = menu.Line(TestMenu.Parfait, 1, OrderTiming.AfterMeal);
        await OrderAsync(hall, visit, menu.Order(salad, parfait));

        // Act
        using var ordered = await hall.PostAsync("/api/v1/serving/serve", new ServeRequest { LineIds = [salad.Id] });
        using var held = await hall.PostAsync("/api/v1/serving/serve", new ServeRequest { LineIds = [parfait.Id] });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, ordered.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, held.StatusCode);
        Assert.Equal("LINE_STATUS_INVALID", await TestDevice.ReadErrorCodeAsync(held));
    }

    // 払い終えて閉じた来店の品も、その営業日のうちは会計済みとして提供の一覧に出て提供でき、前の営業日の来店の品は出ない
    [Fact]
    public async Task ClosedVisitLinesRemainForBusinessDay()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var menu = await TestMenu.LoadAsync(hall);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var previous = await OpenAsync(hall, store.TableIds[1]);
        var salad = menu.Line(TestMenu.Salad);
        await OrderAsync(hall, visit, menu.Order(salad));
        await OrderAsync(hall, previous, menu.Order(menu.Line(TestMenu.Salad)));
        await CloseAsync(hall, visit);
        await CloseAsync(hall, previous);
        await factory.MoveVisitToPreviousDayAsync(store, previous.Id);

        // できあがるのは払い終えたあと (キッチンのチケットは来店を閉じても残る)
        foreach (var ticket in (await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items)
        {
            using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{Assert.Single(ticket.Lines).LineId}/ready", new { });
            ready.EnsureSuccessStatusCode();
        }

        // Act / Assert: 提供の一覧
        var serving = await hall.GetAsync<ServingListResponse>("/api/v1/serving");
        var table = Assert.Single(serving.Items);
        Assert.Equal(visit.Id, table.VisitId);
        Assert.Equal(VisitStatus.Closed, table.VisitStatus);
        Assert.Equal(salad.Id, Assert.Single(table.Lines).LineId);

        // Act / Assert: 提供する
        using var served = await hall.PostAsync("/api/v1/serving/serve", new ServeRequest { LineIds = [salad.Id] });
        Assert.Equal(HttpStatusCode.NoContent, served.StatusCode);
        Assert.Empty((await hall.GetAsync<ServingListResponse>("/api/v1/serving")).Items);
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

    // レジで払ったとして閉じる
    private static async Task CloseAsync(TestDevice device, VisitResponse visit)
    {
        var current = await device.GetAsync<VisitResponse>($"/api/v1/visits/{visit.Id}");
        using var response = await device.PostAsync($"/api/v1/visits/{visit.Id}/close", new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = current.Version });
        response.EnsureSuccessStatusCode();
    }

    private static async Task OrderAsync(TestDevice device, VisitResponse visit, OrderCreateRequest request)
    {
        using var response = await device.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);
        response.EnsureSuccessStatusCode();
    }
}
