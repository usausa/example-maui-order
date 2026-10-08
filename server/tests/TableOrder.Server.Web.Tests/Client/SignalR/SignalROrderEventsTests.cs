namespace TableOrder.Server.Web.Client.SignalR;

using TableOrder.Client;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Visits;

// 端末のアプリの通知の受け口 (SignalR) を、本物のサーバのハブにつないで確かめる
public sealed class SignalROrderEventsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public SignalROrderEventsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // ホールが開いた来店と、人数を直した来店と、ほかのテーブルに移した来店が、このテーブルの端末に届く
    [Fact]
    public async Task VisitEventsReachTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act / Assert: つなぐ
        Assert.True((await terminal.Events.ConnectAsync(TestContext.Current.CancellationToken)).IsSuccess);

        // Act / Assert: ホールが開く
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);
        Assert.Equal(visit.Id, Assert.IsType<VisitOpenedEvent>(await terminal.NextAsync()).Visit.Id);

        // Act / Assert: ホールが人数を直す
        using var updated = await hall.PatchAsync($"/api/v1/visits/{visit.Id}", new VisitUpdateRequest { Adults = 3, Children = 1, Version = visit.Version });
        var changed = await TestDevice.ReadAsync<VisitResponse>(updated);
        var guests = Assert.IsType<VisitUpdatedEvent>(await terminal.NextAsync());
        Assert.Equal(3, guests.Visit.Adults);
        Assert.Equal(1, guests.Visit.Children);

        // Act / Assert: ほかのテーブルに移す
        using var moved = await hall.PostAsync($"/api/v1/visits/{visit.Id}/move", new VisitMoveRequest { ToTableId = store.TableIds[1], Version = changed.Version });
        moved.EnsureSuccessStatusCode();
        Assert.Equal(store.TableIds[1], Assert.IsType<VisitMovedEvent>(await terminal.NextAsync()).Visit.TableId);
    }

    // 品切れの変化と、注文の受け付け・明細の取消が、このテーブルの端末に届く
    [Fact]
    public async Task StockAndOrderEventsReachTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        var cancel = TestContext.Current.CancellationToken;
        var menu = TestMenu.From((await terminal.Table.GetMenuAsync(cancel)).Content!);
        var visit = await factory.OpenVisitAsync(store, 0);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);

        // Act / Assert: ホールが売り切れにする
        using var soldOut = await hall.PutAsync($"/api/v1/stock/{TestMenu.Parfait}", new StockUpdateRequest { TargetKind = StockTargetKind.Item, Status = StockStatus.SoldOut });
        soldOut.EnsureSuccessStatusCode();
        var stock = Assert.IsType<StockUpdatedEvent>(await terminal.NextAsync());
        Assert.Equal(StockStatus.SoldOut, Assert.Single(stock.Items, x => x.TargetId == TestMenu.Parfait).Status);

        // Act / Assert: テーブルが注文する
        var ordered = (await terminal.Table.CreateOrderAsync(visit.Id, menu.Order(menu.Line(TestMenu.Salad)), cancel)).Content!;
        Assert.Equal(ordered.Id, Assert.IsType<OrderCreatedEvent>(await terminal.NextAsync()).Order.Id);

        // Act / Assert: ホールが明細を取り消す
        using var cancelled = await hall.PostAsync($"/api/v1/orders/{ordered.Id}/lines/{ordered.Lines[0].Id}/cancel", new OrderLineCancelRequest { Quantity = 1 });
        cancelled.EnsureSuccessStatusCode();
        var lines = Assert.IsType<OrderLinesUpdatedEvent>(await terminal.NextAsync());
        Assert.Equal(visit.Id, lines.VisitId);
        Assert.Equal(OrderLineStatus.Cancelled, Assert.Single(Assert.Single(lines.Orders).Lines).Status);
    }

    // つないだ時点の番号から数える (つなぐ前の通知は、このあとに読む今の状態に入っているので渡さない)
    [Fact]
    public async Task ConnectStartsFromCurrentSeq()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);

        // Act
        Assert.True((await terminal.Events.ConnectAsync(TestContext.Current.CancellationToken)).IsSuccess);
        using var closed = await hall.PostAsync($"/api/v1/visits/{visit.Id}/cancel", new VisitCancelRequest { Version = visit.Version });
        closed.EnsureSuccessStatusCode();

        // Assert
        var e = Assert.IsType<VisitClosedEvent>(await terminal.NextAsync());
        Assert.Equal(VisitStatus.Cancelled, e.Visit.Status);
    }

    // 登録していない端末はつながない
    [Fact]
    public async Task UnregisteredDeviceDoesNotConnect()
    {
        // Arrange
        await using var terminal = TestTerminal.Create(factory);

        // Act
        var result = await terminal.Events.ConnectAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ApiStatus.Unauthorized, result.Status);
    }
}
