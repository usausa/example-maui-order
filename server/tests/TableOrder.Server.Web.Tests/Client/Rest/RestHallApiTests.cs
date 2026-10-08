namespace TableOrder.Server.Web.Client.Rest;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Calls;
using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Serving;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;

// ホール端末のアプリの REST の窓口を、本物のサーバにつないで確かめる
public sealed class RestHallApiTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public RestHallApiTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 起動で読むもの (店舗、メニュー、品切れ、席、呼び出し、提供) を読める。メニューは版が同じなら前に読んだものを使う
    [Fact]
    public async Task StartupReadsThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.HallCode);
        var api = terminal.Hall;
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var shop = await api.GetStoreAsync(cancel);
        var menu = await api.GetMenuAsync(cancel);
        var again = await api.GetMenuAsync(cancel);
        var stock = await api.GetStockAsync(cancel);
        var tables = await api.GetTablesAsync(cancel: cancel);
        var calls = await api.GetCallsAsync(cancel: cancel);
        var serving = await api.GetServingAsync(cancel: cancel);

        // Assert
        Assert.Equal(store.StoreId, shop.Content!.Id);
        Assert.Same(menu.Content, again.Content);
        Assert.Empty(stock.Content!.Items);
        Assert.Equal(store.TableIds, tables.Content!.Items.Select(static x => x.Id));
        Assert.Empty(calls.Content!.Items);
        Assert.Empty(serving.Content!.Items);
    }

    // 来店の流れ (案内、人数、席の移動、確認、代わりの注文、取消、食後の品のお願い、会計の手伝い、レジで払った) を REST の窓口で通す
    [Fact]
    public async Task VisitFlowThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.HallCode);
        var api = terminal.Hall;
        var cancel = TestContext.Current.CancellationToken;
        var menu = TestMenu.From((await api.GetMenuAsync(cancel)).Content!);

        // Act / Assert: 空いている席に案内し、人数を直して別の席に移す
        Assert.Equal(store.TableIds, (await api.GetTablesAsync(TableStatus.Vacant, cancel)).Content!.Items.Select(static x => x.Id));
        var opened = (await api.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 }, cancel)).Content!;
        var updated = (await api.UpdateVisitAsync(opened.Id, new VisitUpdateRequest { Adults = 3, Children = 1, Version = opened.Version }, cancel)).Content!;
        Assert.Equal((3, 1), (updated.Adults, updated.Children));
        var moved = (await api.MoveVisitAsync(opened.Id, new VisitMoveRequest { ToTableId = store.TableIds[1], Version = updated.Version }, cancel)).Content!;
        Assert.Equal(store.TableIds[1], moved.TableId);
        Assert.Equal([store.TableIds[1]], (await api.GetTablesAsync(TableStatus.Occupied, cancel)).Content!.Items.Select(static x => x.Id));

        // Act / Assert: お客様に確かめてお酒を代わりに注文し、1 杯を取り消す。食後の品はお願いしてから作る
        var confirmed = (await api.ConfirmAsync(opened.Id, new VisitConfirmationRequest { RuleId = TestMenu.AlcoholRuleId }, cancel)).Content!;
        Assert.Equal([TestMenu.AlcoholRuleId], confirmed.ConfirmedRuleIds);
        var beer = menu.Line(TestMenu.Beer, 2);
        var parfait = menu.Line(TestMenu.Parfait, 1, OrderTiming.AfterMeal);
        var order = (await api.CreateOrderAsync(opened.Id, menu.Order(beer, parfait), cancel)).Content!;
        var cancelled = (await api.CancelLineAsync(order.Id, beer.Id, new OrderLineCancelRequest { Quantity = 1 }, cancel)).Content!;
        Assert.Equal(1, cancelled.Lines.Single(x => x.Id == beer.Id).Quantity);
        var released = (await api.ReleaseAsync(opened.Id, new OrderReleaseRequest { LineIds = [parfait.Id] }, cancel)).Content!;
        Assert.Equal(OrderLineStatus.Ordered, released.Items.SelectMany(static x => x.Lines).Single(x => x.Id == parfait.Id).Status);

        // Act / Assert: 会計を始めて取りやめ、レジで払って閉じる
        var bill = (await api.GetBillAsync(opened.Id, cancel)).Content!;
        var current = (await api.GetVisitAsync(opened.Id, cancel)).Content!;
        var paying = (await api.StartCheckoutAsync(opened.Id, new CheckoutRequest { BillVersion = bill.BillVersion, Version = current.Version }, cancel)).Content!;
        Assert.Equal(VisitStatus.Paying, paying.Status);
        var reopened = (await api.CancelCheckoutAsync(opened.Id, cancel)).Content!;
        Assert.Equal(VisitStatus.Open, reopened.Status);
        var closed = (await api.CloseVisitAsync(opened.Id, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = reopened.Version }, cancel)).Content!;
        Assert.Equal((VisitStatus.Closed, VisitClosedBy.Register), (closed.Status, closed.ClosedBy));
        Assert.Empty((await api.GetTablesAsync(TableStatus.Occupied, cancel)).Content!.Items);
    }

    // テーブルの呼び出しに向かって対応を終え、キッチンができあがりにした品を提供する
    [Fact]
    public async Task CallsAndServingThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.HallCode);
        await using var table = TestTerminal.Create(factory);
        await table.PairAsync(store.TableCodes[0]);
        using var kitchen = new TestDevice(factory.CreateClient());
        await kitchen.SignInAsync(store.KitchenCode);
        var api = terminal.Hall;
        var cancel = TestContext.Current.CancellationToken;
        var menu = TestMenu.From((await api.GetMenuAsync(cancel)).Content!);
        var visit = (await api.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 }, cancel)).Content!;

        // Act / Assert: 呼び出しに向かい、対応を終える
        var call = (await table.Table.CreateCallAsync(visit.Id, new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" }, cancel)).Content!;
        Assert.Equal(call.Id, Assert.Single((await api.GetCallsAsync(cancel: cancel)).Content!.Items).Id);
        Assert.Equal(CallStatus.Acknowledged, (await api.AcknowledgeCallAsync(call.Id, cancel)).Content!.Status);
        Assert.Equal(CallStatus.Done, (await api.CompleteCallAsync(call.Id, cancel)).Content!.Status);
        Assert.Empty((await api.GetCallsAsync(cancel: cancel)).Content!.Items);
        Assert.Equal(call.Id, Assert.Single((await api.GetCallsAsync(CallStatus.Done, cancel)).Content!.Items).Id);

        // Act / Assert: できあがった品を提供すると、提供の一覧から消える
        var salad = menu.Line(TestMenu.Salad);
        Assert.True((await api.CreateOrderAsync(visit.Id, menu.Order(salad), cancel)).IsSuccess);
        var ticket = Assert.Single((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        using var ready = await kitchen.PostAsync($"/api/v1/kitchen/tickets/{ticket.Id}/lines/{salad.Id}/ready", new { });
        ready.EnsureSuccessStatusCode();
        Assert.Equal(salad.Id, Assert.Single(Assert.Single((await api.GetServingAsync(cancel: cancel)).Content!.Items).Lines).LineId);
        Assert.True((await api.ServeAsync(new ServeRequest { LineIds = [salad.Id] }, cancel)).IsSuccess);
        Assert.Empty((await api.GetServingAsync(cancel: cancel)).Content!.Items);
    }

    // 品切れと残りの数を変えてすべて戻し、注文を一時停止して再開する。注文のないまま帰った来店は取りやめる
    [Fact]
    public async Task StockOrderingAndCancelThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.HallCode);
        var api = terminal.Hall;
        var cancel = TestContext.Current.CancellationToken;

        // Act / Assert: 売り切れと残りの数を変えて、すべて戻す
        Assert.True((await api.UpdateStockAsync(TestMenu.Salad, new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut }, cancel)).IsSuccess);
        Assert.True((await api.UpdateStockAsync(TestMenu.Parfait, new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.Limited, Remaining = 3 }, cancel)).IsSuccess);
        var stock = (await api.GetStockAsync(cancel)).Content!;
        Assert.Equal(StockStatus.SoldOut, stock.Items.Single(static x => x.TargetId == TestMenu.Salad).Status);
        Assert.Equal(3, stock.Items.Single(static x => x.TargetId == TestMenu.Parfait).Remaining);
        Assert.True((await api.ResetStockAsync(cancel)).IsSuccess);
        Assert.Empty((await api.GetStockAsync(cancel)).Content!.Items);

        // Act / Assert: 注文を一時停止して再開する
        Assert.True((await api.SetOrderingAsync(new StoreOrderingRequest { Paused = true, Message = new LocalizedText { Ja = "ただいま混み合っています" } }, cancel)).IsSuccess);
        Assert.True((await api.GetStoreAsync(cancel)).Content!.OrderingPaused);
        Assert.True((await api.SetOrderingAsync(new StoreOrderingRequest { Paused = false }, cancel)).IsSuccess);
        Assert.False((await api.GetStoreAsync(cancel)).Content!.OrderingPaused);

        // Act / Assert: 注文のないまま帰った来店を取りやめる
        var visit = (await api.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[2], Adults = 1 }, cancel)).Content!;
        Assert.Equal(VisitStatus.Cancelled, (await api.CancelVisitAsync(visit.Id, new VisitCancelRequest { Version = visit.Version }, cancel)).Content!.Status);
    }
}
