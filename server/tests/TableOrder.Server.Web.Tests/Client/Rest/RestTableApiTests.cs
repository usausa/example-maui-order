namespace TableOrder.Server.Web.Client.Rest;

using TableOrder.Client;
using TableOrder.Contract.Bills;
using TableOrder.Contract.Calls;
using TableOrder.Contract.Payments;
using TableOrder.Contract.Visits;

// テーブル端末のアプリの REST の窓口を、本物のサーバにつないで確かめる
public sealed class RestTableApiTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public RestTableApiTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 起動で読むもの (店舗、メニュー、品切れ、今の来店) を読める。メニューは版が同じなら前に読んだものを使う
    [Fact]
    public async Task StartupReadsThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var shop = await terminal.Table.GetStoreAsync(cancel);
        var menu = await terminal.Table.GetMenuAsync(cancel);
        var again = await terminal.Table.GetMenuAsync(cancel);
        var stock = await terminal.Table.GetStockAsync(cancel);
        var visit = await terminal.Table.GetCurrentVisitAsync(cancel);

        // Assert
        Assert.Equal(store.StoreId, shop.Content!.Id);
        Assert.Same(menu.Content, again.Content);
        Assert.Empty(stock.Content!.Items);
        Assert.True(visit.IsSuccess);
        Assert.Null(visit.Content);
    }

    // 来店の開き方が席の店では、テーブル端末が人数を入れて自分のテーブルに来店を開く。スタッフの店では断られる
    [Fact]
    public async Task StartVisitThroughRest()
    {
        // Arrange
        var tableStore = await factory.CreateStoreAsync(VisitOpening.Table);
        var hallStore = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(tableStore.TableCodes[0]);
        await using var other = TestTerminal.Create(factory);
        await other.PairAsync(hallStore.TableCodes[0]);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var started = await terminal.Table.StartVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 2, Children = 1 }, cancel);
        var rejected = await other.Table.StartVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 2 }, cancel);

        // Assert
        var visit = started.Content!;
        Assert.Equal((tableStore.TableIds[0], VisitOpenedBy.Table), (visit.TableId, visit.OpenedBy));
        Assert.Equal(visit.Id, (await terminal.Table.GetCurrentVisitAsync(cancel)).Content!.Id);
        Assert.Equal(ApiStatus.Rejected, rejected.Status);
        Assert.Equal("VISIT_OPENING_DISABLED", rejected.ErrorCode);
    }

    // テーブル端末の流れ (来店、確認、注文、呼び出し、会計、支払、電子レシート) を REST の窓口で通す
    [Fact]
    public async Task TableFlowThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        var api = terminal.Table;
        var cancel = TestContext.Current.CancellationToken;
        var menu = TestMenu.From((await api.GetMenuAsync(cancel)).Content!);

        // Act / Assert: スタッフが開いた来店で、お酒の確認に答えて注文する
        var visit = await factory.OpenVisitAsync(store, 0);
        var confirmed = await api.ConfirmAsync(visit.Id, new VisitConfirmationRequest { RuleId = TestMenu.AlcoholRuleId }, cancel);
        Assert.Equal([TestMenu.AlcoholRuleId], confirmed.Content!.ConfirmedRuleIds);
        var ordered = await api.CreateOrderAsync(visit.Id, menu.Order(menu.Line(TestMenu.Beer, 2)), cancel);
        Assert.Equal(OrderLineStatus.Ordered, Assert.Single(ordered.Content!.Lines).Status);
        Assert.Single((await api.GetOrdersAsync(visit.Id, cancel)).Content!.Items);

        // Act / Assert: 呼び出す
        var call = await api.CreateCallAsync(visit.Id, new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" }, cancel);
        Assert.Equal(CallStatus.Open, call.Content!.Status);
        Assert.Single((await api.GetCallsAsync(visit.Id, cancel)).Content!.Items);

        // Act / Assert: 会計を始めて QR で払い、決済サービスの通知で払い終えると来店が閉じる
        var bill = (await api.GetBillAsync(visit.Id, cancel)).Content!;
        var paying = await api.StartCheckoutAsync(visit.Id, new CheckoutRequest { BillVersion = bill.BillVersion, Version = confirmed.Content.Version }, cancel);
        Assert.Equal(VisitStatus.Paying, paying.Content!.Status);
        var payment = (await api.CreatePaymentAsync(visit.Id, new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = bill.Total }, cancel)).Content!;
        using var callback = factory.CreateClient();
        using var completed = await callback.PostAsJsonAsync("/api/v1/payments/callbacks/fake", new PaymentCallbackRequest { ProviderReference = payment.Id.ToString("N"), Status = PaymentStatus.Completed }, TestDevice.JsonOptions, cancel);
        completed.EnsureSuccessStatusCode();
        Assert.Equal(PaymentStatus.Completed, (await api.GetPaymentAsync(payment.Id, cancel)).Content!.Status);
        Assert.Null((await api.GetCurrentVisitAsync(cancel)).Content);
        Assert.Equal(bill.Total, (await api.GetReceiptAsync(visit.Id, cancel)).Content!.Total);
    }

    // 業務のルールで断られたら、errorCode と文言を返す
    [Fact]
    public async Task RejectionCarriesErrorCode()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        var cancel = TestContext.Current.CancellationToken;
        var menu = TestMenu.From((await terminal.Table.GetMenuAsync(cancel)).Content!);
        var visit = await factory.OpenVisitAsync(store, 0, adults: 1);
        var line = menu.Line(TestMenu.Salad);
        line.UnitPrice += 1;

        // Act
        var result = await terminal.Table.CreateOrderAsync(visit.Id, menu.Order(line), cancel);

        // Assert
        Assert.Equal(ApiStatus.Rejected, result.Status);
        Assert.Equal("MENU_CHANGED", result.ErrorCode);
        Assert.False(String.IsNullOrEmpty(result.Detail));
    }
}
