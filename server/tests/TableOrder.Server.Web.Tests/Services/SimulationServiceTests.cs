namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Calls;
using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Payments;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Services;
using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Settings;

// 開発の環境の自動の進行 (サーバの中の SimulationService を、時間を 0 にして店舗の文脈で呼ぶ)
public sealed class SimulationServiceTests : IClassFixture<ServerFactory>
{
    private static readonly SimulationTiming Immediately = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

    private readonly ServerFactory factory;

    public SimulationServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 明細は提供まで進んでチケットは下がり、呼び出しは向かってから対応を終え、待っている支払は払い終えて来店が閉じる
    [Fact]
    public async Task AdvanceMovesLinesCallsAndPayments()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);
        using var kitchen = new TestDevice(factory.CreateClient());
        await kitchen.SignInAsync(store.KitchenCode);
        var visit = await factory.OpenVisitAsync(store, 0, adults: 1);
        var menu = await TestMenu.LoadAsync(table);
        using var ordered = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));
        ordered.EnsureSuccessStatusCode();
        using var called = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Staff" });
        called.EnsureSuccessStatusCode();

        // Act / Assert: 明細は提供まで進み、チケットは下がる。呼び出しには向かう
        await AdvanceAsync(store);
        var orders = await table.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.Equal(OrderLineStatus.Served, Assert.Single(orders.Items[0].Lines).Status);
        Assert.Empty((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        Assert.Equal(CallStatus.Acknowledged, Assert.Single((await table.GetAsync<CallListResponse>($"/api/v1/visits/{visit.Id}/calls")).Items).Status);

        // Act / Assert: 呼び出しの対応を終える
        await AdvanceAsync(store);
        Assert.Equal(CallStatus.Done, Assert.Single((await table.GetAsync<CallListResponse>($"/api/v1/visits/{visit.Id}/calls")).Items).Status);

        // Act / Assert: 待っている支払を払い終え、来店が閉じる
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });
        checkout.EnsureSuccessStatusCode();
        using var paid = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = bill.Total });
        paid.EnsureSuccessStatusCode();
        await AdvanceAsync(store);
        using var current = await table.Client.GetAsync(new Uri("/api/v1/devices/me/visit", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    // 払い終えて閉じた来店の品も、提供まで進める (キッチンは作り続け、ホールはお席に運ぶ)
    [Fact]
    public async Task AdvanceMovesLinesOfClosedVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(hall);
        using var ordered = await hall.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));
        ordered.EnsureSuccessStatusCode();
        using var closed = await hall.PostAsync($"/api/v1/visits/{visit.Id}/close", new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version });
        closed.EnsureSuccessStatusCode();

        // Act
        await AdvanceAsync(store);

        // Assert
        var orders = await hall.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.Equal(OrderLineStatus.Served, Assert.Single(orders.Items[0].Lines).Status);
    }

    // テストのサーバは自動の進行を止める (時間で進んで、ほかのテストの結果を変えないように)
    [Fact]
    public void SimulationIsStoppedInTests()
    {
        // Act
        var setting = factory.Services.GetRequiredService<SimulationSetting>();

        // Assert
        Assert.False(setting.Enabled);
    }

    // 裏の処理と同じく、テナントと店舗の文脈を始めて進める
    private async Task AdvanceAsync(TestStore store)
    {
        var provider = factory.Services.GetRequiredService<ApplicationServiceContextProvider>();
        var simulation = factory.Services.GetRequiredService<SimulationService>();
        Assert.Contains(await simulation.GetStoreAllAsync(TestContext.Current.CancellationToken), x => x.StoreId == store.StoreId);
        using var scope = provider.Begin(() => new ServiceContext(DateTimeOffset.UtcNow) { TenantId = store.TenantId, StoreId = store.StoreId });
        await simulation.AdvanceAsync(Immediately, TestContext.Current.CancellationToken);
    }
}
