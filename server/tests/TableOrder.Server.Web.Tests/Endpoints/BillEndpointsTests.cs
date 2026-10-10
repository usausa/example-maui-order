namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Kitchen;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Visits;

public sealed class BillEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public BillEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 同じ商品とオプションの明細はまとめ、取消を除いて、税率ごとの内税と人数で割った目安を出す
    [Fact]
    public async Task BillGroupsLinesAndTaxes()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var hall = await SignInAsync(store.HallCode);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var first = menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.CheeseSauce);
        var second = menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.CheeseSauce);
        var salad = menu.Line(TestMenu.Salad);
        await OrderAsync(table, visit, menu.Order(first, salad));
        var order = await OrderAsync(table, visit, menu.Order(second));
        using var cancelled = await hall.PostAsync($"/api/v1/orders/{order.Id}/lines/{second.Id}/cancel", new OrderLineCancelRequest { Quantity = 1 });
        cancelled.EnsureSuccessStatusCode();
        using var third = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.CheeseSauce)));
        third.EnsureSuccessStatusCode();

        // Act
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");

        // Assert
        var total = (first.UnitPrice * 2) + salad.UnitPrice;
        Assert.Equal(2, bill.Lines.Count);
        Assert.Equal(2, bill.Lines.Single(x => x.UnitPrice == first.UnitPrice).Quantity);
        Assert.Equal(total, bill.Total);
        Assert.Equal(total, bill.Balance);
        var tax = Assert.Single(bill.Taxes);
        Assert.Equal(Pricing.IncludedTax(total, tax.Rate, TaxRounding.Floor), tax.TaxAmount);
        Assert.Equal(Pricing.Split(total, 2), bill.SplitAmounts);
        Assert.True(bill.HasUnservedLines);
    }

    // 税率だけが違う明細 (税率を直したメニューの前後に頼んだ同じ品) はまとめず、税率ごとに内税を出す
    [Fact]
    public async Task LinesWithDifferentTaxRatesAreNotGrouped()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var before = menu.Line(TestMenu.Salad);
        var after = menu.Line(TestMenu.Salad);
        await OrderAsync(table, visit, menu.Order(before));
        await OrderAsync(table, visit, menu.Order(after));
        await factory.SetLineTaxRateAsync(store, before.Id, 0.08m);

        // Act
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");

        // Assert
        Assert.Equal(2, bill.Lines.Count);
        Assert.Equal([0.08m, 0.10m], bill.Lines.Select(static x => x.TaxRate).Order());
        Assert.Equal(2, bill.Taxes.Count);
        Assert.All(bill.Taxes, x => Assert.Equal(Pricing.IncludedTax(before.UnitPrice, x.Rate, TaxRounding.Floor), x.TaxAmount));
    }

    // 会計を始めると注文を止め、やめると注文できる状態に戻す。明細が変わっていれば始めない
    [Fact]
    public async Task CheckoutStopsOrdering()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        await OrderAsync(table, visit, menu.Order(menu.Line(TestMenu.Salad)));
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");

        // Act / Assert: 明細が変わった
        using var changed = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = "old", Version = visit.Version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("BILL_CHANGED", await TestDevice.ReadErrorCodeAsync(changed));

        // Act / Assert: 会計を始めると注文できない (送り直しは会計中のまま返す)
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });
        Assert.Equal(VisitStatus.Paying, (await TestDevice.ReadAsync<VisitResponse>(checkout)).Status);
        using var resent = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });
        Assert.Equal(VisitStatus.Paying, (await TestDevice.ReadAsync<VisitResponse>(resent)).Status);
        using var paying = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));
        Assert.Equal("CHECKOUT_IN_PROGRESS", await TestDevice.ReadErrorCodeAsync(paying));

        // Act / Assert: やめると注文できる
        using var cancel = await table.Client.PostAsync(new Uri($"/api/v1/visits/{visit.Id}/checkout/cancel", UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(VisitStatus.Open, (await TestDevice.ReadAsync<VisitResponse>(cancel)).Status);
        using var reopened = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad)));
        Assert.Equal(HttpStatusCode.Created, reopened.StatusCode);
    }

    // 会計を始めると、お願いしていない食後の品をキッチンにお願いする (払い終えると来店は閉じ、あとからお願いできない)
    [Fact]
    public async Task CheckoutReleasesHeldLines()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        var parfait = menu.Line(TestMenu.Parfait, 1, OrderTiming.AfterMeal);
        await OrderAsync(table, visit, menu.Order(parfait));
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");

        // Act
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });

        // Assert
        Assert.Equal(VisitStatus.Paying, (await TestDevice.ReadAsync<VisitResponse>(checkout)).Status);
        var orders = await table.GetAsync<OrderListResponse>($"/api/v1/visits/{visit.Id}/orders");
        Assert.Equal(OrderLineStatus.Ordered, Assert.Single(orders.Items[0].Lines).Status);
        var ticket = Assert.Single((await kitchen.GetAsync<KitchenTicketListResponse>("/api/v1/kitchen/tickets")).Items);
        Assert.Equal(parfait.Id, Assert.Single(ticket.Lines).LineId);
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    private static async Task<OrderListResponseItem> OrderAsync(TestDevice device, VisitResponse visit, OrderCreateRequest request)
    {
        using var response = await device.PostAsync($"/api/v1/visits/{visit.Id}/orders", request);
        return await TestDevice.ReadAsync<OrderListResponseItem>(response);
    }
}
