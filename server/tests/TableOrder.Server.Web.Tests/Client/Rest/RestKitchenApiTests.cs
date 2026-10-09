namespace TableOrder.Server.Web.Client.Rest;

using TableOrder.Client;
using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Visits;

// キッチン端末のアプリの REST の窓口を、本物のサーバにつないで確かめる
public sealed class RestKitchenApiTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public RestKitchenApiTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 起動で読むもの (店舗、メニューの持ち場、品切れ、チケット) を読める。メニューは版が同じなら前に読んだものを使う
    [Fact]
    public async Task StartupReadsThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.KitchenCode);
        var api = terminal.Kitchen;
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var shop = await api.GetStoreAsync(cancel);
        var menu = await api.GetMenuAsync(cancel);
        var again = await api.GetMenuAsync(cancel);
        var stock = await api.GetStockAsync(cancel);
        var tickets = await api.GetTicketsAsync(cancel: cancel);

        // Assert
        Assert.Equal(store.StoreId, shop.Content!.Id);
        Assert.Same(menu.Content, again.Content);
        Assert.Contains(menu.Content!.Stations, static x => x.Id == TestMenu.KitchenStation);
        Assert.Empty(stock.Content!.Items);
        Assert.Empty(tickets.Content!.Items);
    }

    // 注文でできたチケットを通知で知り、作り始め・できあがり・下げる・戻すを REST の窓口で通す。受け持たない持ち場は断られる
    [Fact]
    public async Task TicketFlowThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var hall = TestTerminal.Create(factory);
        await hall.PairAsync(store.HallCode);
        await using var kitchen = TestTerminal.Create(factory);
        await kitchen.PairAsync(store.KitchenCode);
        var api = kitchen.Kitchen;
        var cancel = TestContext.Current.CancellationToken;
        Assert.True((await kitchen.Events.ConnectAsync(cancel)).IsSuccess);
        var menu = TestMenu.From((await hall.Hall.GetMenuAsync(cancel)).Content!);
        var visit = (await hall.Hall.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 }, cancel)).Content!;
        var salad = menu.Line(TestMenu.Salad);
        var hamburg = menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.Demiglace);
        Assert.True((await hall.Hall.CreateOrderAsync(visit.Id, menu.Order(salad, hamburg), cancel)).IsSuccess);

        // Act / Assert: チケットができた通知を受け、受け持つ持ち場のチケットを読める
        Assert.IsType<TicketCreatedEvent>(await kitchen.NextAsync());
        var ticket = Assert.Single((await api.GetTicketsAsync(TestMenu.KitchenStation, cancel: cancel)).Content!.Items);

        // Act / Assert: 作り始めでチケットが変わった通知を受け、できあがり、下げる (残りの明細もできあがり)、戻すを通す
        Assert.Equal(OrderLineStatus.Cooking, Line((await api.StartLineAsync(ticket.Id, salad.Id, cancel)).Content!, salad.Id).Status);
        Assert.IsType<TicketUpdatedEvent>(await kitchen.NextAsync());
        Assert.Equal(OrderLineStatus.Ready, Line((await api.ReadyLineAsync(ticket.Id, salad.Id, cancel)).Content!, salad.Id).Status);
        var done = (await api.BumpAsync(ticket.Id, cancel)).Content!;
        Assert.Equal(KitchenTicketStatus.Done, done.Status);
        Assert.All(done.Lines, static x => Assert.Equal(OrderLineStatus.Ready, x.Status));
        Assert.Equal([ticket.Id], (await api.GetTicketsAsync(status: KitchenTicketStatus.Done, cancel: cancel)).Content!.Items.Select(static x => x.Id));
        Assert.Equal(KitchenTicketStatus.Open, (await api.RecallAsync(ticket.Id, cancel)).Content!.Status);

        // Act / Assert: 受け持たない持ち場は断られる
        var other = await api.GetTicketsAsync(Guid.CreateVersion7(), cancel: cancel);
        Assert.Equal(ApiStatus.Rejected, other.Status);
        Assert.Equal("DEVICE_SCOPE", other.ErrorCode);
    }

    // キッチン端末からも品切れと残りの数を設定できる
    [Fact]
    public async Task StockUpdateThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.KitchenCode);
        var api = terminal.Kitchen;
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var soldOut = await api.UpdateStockAsync(TestMenu.Salad, new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut }, cancel);
        var limited = await api.UpdateStockAsync(TestMenu.Parfait, new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.Limited, Remaining = 2 }, cancel);

        // Assert
        Assert.True(soldOut.IsSuccess);
        Assert.True(limited.IsSuccess);
        var stock = (await api.GetStockAsync(cancel)).Content!;
        Assert.Equal(StockStatus.SoldOut, stock.Items.Single(static x => x.TargetId == TestMenu.Salad).Status);
        Assert.Equal(2, stock.Items.Single(static x => x.TargetId == TestMenu.Parfait).Remaining);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static KitchenTicketListResponseLine Line(KitchenTicketListResponseItem ticket, Guid lineId) =>
        ticket.Lines.Single(x => x.LineId == lineId);
}
