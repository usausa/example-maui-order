namespace TableOrder.Server.Web.Endpoints;

using System.Text.Json;

using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;

public sealed class OrderEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public OrderEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // テーブル端末の注文を受け、注文履歴と来店の合計に出す
    [Fact]
    public async Task TableOrdersAndHistoryShowsLines()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var request = menu.Order(menu.Line(TestMenu.Hamburg, 2, OrderTiming.Now, TestMenu.CheeseSauce), menu.Line(TestMenu.Salad));

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await TestDevice.ReadAsync<OrderListResponseItem>(response);
        Assert.Equal(1, order.OrderNo);
        Assert.Equal(OrderSource.Table, order.Source);
        Assert.Equal(visit.Id, order.VisitId);
        Assert.Equal(request.Lines.Sum(static x => x.UnitPrice * x.Quantity), order.Amount);
        Assert.All(order.Lines, static x => Assert.Equal(OrderLineStatus.Ordered, x.Status));
        Assert.Equal([TestMenu.CheeseSauce], order.Lines[0].Options.Select(static x => x.OptionId));
        var history = await table.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.Equal([order.Id], history.Items.Select(static x => x.Id));
        Assert.Equal(order.Amount, (await table.GetAsync<VisitResponse>("/api/v1/devices/me/visit")).OrderTotal);
    }

    // 同じ Id の送り直しは受け付けた注文を返し、内容が違えば受けない
    [Fact]
    public async Task ResendReturnsAcceptedOrder()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var request = menu.Order(menu.Line(TestMenu.Salad));
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);

        // Act
        using var resent = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);
        request.Lines[0].Quantity = 2;
        using var changed = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        Assert.Equal(request.Id, (await TestDevice.ReadAsync<OrderListResponseItem>(resent)).Id);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("DUPLICATE_ID_MISMATCH", await TestDevice.ReadErrorCodeAsync(changed));
        Assert.Single((await table.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders")).Items);
    }

    // 表示していた単価が今のメニューと違えば受けず、明細の Id で知らせる
    [Fact]
    public async Task PriceMismatchIsMenuChanged()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var line = menu.Line(TestMenu.Salad);
        line.UnitPrice += 1;

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(line));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var (errorCode, keys) = await ReadProblemAsync(response);
        Assert.Equal("MENU_CHANGED", errorCode);
        Assert.Equal([line.Id.ToString()], keys);
    }

    // 明細の一覧に null があれば、入力の誤りとして受けない (500 にしない)
    [Fact]
    public async Task NullLineIsInvalid()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var order = menu.Order(menu.Line(TestMenu.Salad));

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", new { order.Id, order.MenuVersion, Lines = new OrderCreateRequestLine?[] { null } });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 必ず選ぶオプションがなければ受けない
    [Fact]
    public async Task MissingRequiredOptionIsInvalid()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Hamburg)));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("OPTION_INVALID", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 残りの数は注文で減り、なくなると売り切れになる。残りを超える注文と、売り切れの品は受けない
    [Fact]
    public async Task LimitedStockIsReducedUntilSoldOut()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var limited = await hall.PutAsync($"/api/v1/stock/{TestMenu.Salad}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.Limited, Remaining = 2 });
        limited.EnsureSuccessStatusCode();
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var menu = await TestMenu.LoadAsync(hall);

        // Act / Assert: 残りを超える
        using var insufficient = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad, 3)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, insufficient.StatusCode);
        Assert.Equal("STOCK_INSUFFICIENT", await TestDevice.ReadErrorCodeAsync(insufficient));

        // Act / Assert: 残りをすべて頼むと売り切れになる
        using var ordered = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad, 2)));
        Assert.Equal(HttpStatusCode.Created, ordered.StatusCode);
        var stock = await hall.GetAsync<StockResponse>("/api/v1/stock");
        Assert.Equal(StockStatus.SoldOut, stock.Items.Single(static x => x.TargetId == TestMenu.Salad).Status);
        using var soldOut = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, soldOut.StatusCode);
        Assert.Equal("ITEM_SOLD_OUT", await TestDevice.ReadErrorCodeAsync(soldOut));
    }

    // お酒は来店で確認に答えるまで受けない
    [Fact]
    public async Task AlcoholRequiresConfirmation()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);

        // Act / Assert: 答える前
        using var rejected = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Beer)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("CONFIRMATION_REQUIRED", await TestDevice.ReadErrorCodeAsync(rejected));

        // Act / Assert: 答えたあと
        using var confirmed = await table.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = TestMenu.AlcoholRuleId });
        confirmed.EnsureSuccessStatusCode();
        using var accepted = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Beer)));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    // お一人様 1 点までの品は、来店のこれまでの注文と合わせて人数を超えない
    [Fact]
    public async Task LimitPerGuestCountsVisitOrders()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);

        // Act / Assert: 2 人で 3 点は超える
        using var exceeded = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Sirloin, 3, OrderTiming.Now, TestMenu.Medium)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exceeded.StatusCode);
        Assert.Equal("LIMIT_EXCEEDED", await TestDevice.ReadErrorCodeAsync(exceeded));

        // Act / Assert: 2 点は受け、そのあとの 1 点は超える
        using var accepted = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Sirloin, 2, OrderTiming.Now, TestMenu.Medium)));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var more = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Sirloin, 1, OrderTiming.Now, TestMenu.Medium)));
        Assert.Equal("LIMIT_EXCEEDED", await TestDevice.ReadErrorCodeAsync(more));
    }

    // 注文を止めている間は受けない
    [Fact]
    public async Task PausedOrderingIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var menu = await TestMenu.LoadAsync(hall);
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Act
        using var response = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ORDERING_PAUSED", await TestDevice.ReadErrorCodeAsync(response));
    }

    // お客様がとる品は受けたときに提供済み、食後の品はお願いするまで止める
    [Fact]
    public async Task LineStatusDependsOnServingAndTiming()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var request = menu.Order(menu.Line(TestMenu.DrinkBar), menu.Line(TestMenu.Parfait, 1, OrderTiming.AfterMeal));

        // Act / Assert: 受けたとき
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);
        var order = await TestDevice.ReadAsync<OrderListResponseItem>(created);
        Assert.Equal([OrderLineStatus.Served, OrderLineStatus.Held], order.Lines.Select(static x => x.Status));

        // Act / Assert: 食後の品をお願いする
        using var released = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders/release", new OrderReleaseRequest { LineIds = [] });
        var orders = await TestDevice.ReadAsync<OrderListResponse>(released);
        Assert.Equal([OrderLineStatus.Served, OrderLineStatus.Ordered], orders.Items[0].Lines.Select(static x => x.Status));
    }

    //--------------------------------------------------------------------------------
    // Cancel
    //--------------------------------------------------------------------------------

    // 数量の一部の取消は、取り消す分を別の明細に分ける。提供した明細は取り消せない
    [Fact]
    public async Task HallCancelsPartOfLine()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var menu = await TestMenu.LoadAsync(hall);
        var salad = menu.Line(TestMenu.Salad, 3);
        var drinkBar = menu.Line(TestMenu.DrinkBar);
        using var created = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(salad, drinkBar));
        var order = await TestDevice.ReadAsync<OrderListResponseItem>(created);

        // Act
        using var cancelled = await hall.PostAsync($"/api/v1/orders/{order.Id}/lines/{salad.Id}/cancel", new OrderLineCancelRequest { Quantity = 1, Reason = "作り間違い" });
        using var served = await hall.PostAsync($"/api/v1/orders/{order.Id}/lines/{drinkBar.Id}/cancel", new OrderLineCancelRequest { Quantity = 1 });

        // Assert
        var result = await TestDevice.ReadAsync<OrderListResponseItem>(cancelled);
        Assert.Equal(2, result.Lines.Single(x => x.Id == salad.Id).Quantity);
        var split = result.Lines.Single(static x => x.Status == OrderLineStatus.Cancelled);
        Assert.Equal(1, split.Quantity);
        Assert.Equal("作り間違い", split.CancelReason);
        Assert.Equal((salad.UnitPrice * 2) + drinkBar.UnitPrice, result.Amount);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, served.StatusCode);
        Assert.Equal("LINE_STATUS_INVALID", await TestDevice.ReadErrorCodeAsync(served));
    }

    // テーブル端末は取り消せない
    [Fact]
    public async Task TableDeviceCannotCancelLine()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var line = menu.Line(TestMenu.Salad);
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(line));
        var order = await TestDevice.ReadAsync<OrderListResponseItem>(created);

        // Act
        using var response = await table.PostAsync($"/api/v1/orders/{order.Id}/lines/{line.Id}/cancel", new OrderLineCancelRequest { Quantity = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

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

    // Problem Details の errorCode と、errors のキー (明細の Id)
    private static async Task<(string? ErrorCode, List<string> Keys)> ReadProblemAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        var root = document.RootElement;
        return (root.GetProperty("errorCode").GetString(), root.GetProperty("errors").EnumerateObject().Select(static x => x.Name).ToList());
    }
}
