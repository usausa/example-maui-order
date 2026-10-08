namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Payments;
using TableOrder.Contract.Visits;

public sealed class PaymentEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public PaymentEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // QR コード決済は決済サービスの通知で払い終え、残りがなくなると来店を閉じて電子レシートを出す
    [Fact]
    public async Task QrPaymentCompletesByCallbackAndClosesVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var hall = await SignInAsync(store.HallCode);
        var (visit, bill) = await CheckoutAsync(store, table);

        // Act / Assert: 残りを超える額は受けない
        using var over = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = bill.Total + 1 });
        Assert.Equal("PAYMENT_AMOUNT_INVALID", await TestDevice.ReadErrorCodeAsync(over));

        // Act / Assert: QR を出す
        var id = Guid.CreateVersion7();
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = id, Method = PaymentMethod.QrCode, Amount = bill.Total });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payment = await TestDevice.ReadAsync<PaymentResponse>(created);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.NotNull(payment.QrCode);
        Assert.NotNull(payment.ExpiresAt);

        // Act / Assert: 決済サービスの通知 (匿名) で払い終える
        using var anonymous = factory.CreateClient();
        using var callback = await anonymous.PostAsJsonAsync("/api/v1/payments/callbacks/fake", new PaymentCallbackRequest { ProviderReference = id.ToString("N"), Status = PaymentStatus.Completed }, TestDevice.JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, callback.StatusCode);
        Assert.Equal(PaymentStatus.Completed, (await table.GetAsync<PaymentResponse>($"/api/v1/payments/{id}")).Status);
        var closed = await hall.GetAsync<VisitResponse>($"/api/v1/visits/{visit.Id}");
        Assert.Equal(VisitStatus.Closed, closed.Status);
        Assert.Equal(VisitClosedBy.TablePayment, closed.ClosedBy);
        var receipt = await table.GetAsync<ReceiptResponse>($"/api/v1/visits/{visit.Id}/receipt");
        Assert.Contains("/receipts/", receipt.Url.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(bill.Total, receipt.Total);
    }

    // 割り勘はカードで何回かに分けて払い、残りがなくなったら来店を閉じる
    [Fact]
    public async Task SplitCardPaymentsCloseVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var (visit, bill) = await CheckoutAsync(store, table);
        var half = bill.SplitAmounts[0];

        // Act / Assert: 1 人目
        var first = await PayByCardAsync(table, visit, half);
        Assert.Equal(PaymentStatus.Completed, first.Status);
        Assert.Equal(VisitStatus.Paying, (await table.GetAsync<VisitResponse>("/api/v1/devices/me/visit")).Status);

        // Act / Assert: 2 人目で閉じる
        await PayByCardAsync(table, visit, bill.Total - half);
        using var current = await table.Client.GetAsync(new Uri("/api/v1/devices/me/visit", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    // 待っている支払をやめると、その額をまた払える。会計をやめると待っている支払もやめる
    [Fact]
    public async Task CancelReleasesPendingAmount()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var (visit, bill) = await CheckoutAsync(store, table);
        var first = await CreateQrAsync(table, visit, bill.Total);

        // Act / Assert: 待っている分は払えない
        using var blocked = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = bill.Total });
        Assert.Equal("PAYMENT_AMOUNT_INVALID", await TestDevice.ReadErrorCodeAsync(blocked));

        // Act / Assert: やめると払える
        using var cancelled = await table.PostAsync($"/api/v1/payments/{first.Id}/cancel", new { });
        Assert.Equal(PaymentStatus.Cancelled, (await TestDevice.ReadAsync<PaymentResponse>(cancelled)).Status);
        var second = await CreateQrAsync(table, visit, bill.Total);

        // Act / Assert: 会計をやめると待っている支払もやめる
        using var reopened = await table.Client.PostAsync(new Uri($"/api/v1/visits/{visit.Id}/checkout/cancel", UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(VisitStatus.Open, (await TestDevice.ReadAsync<VisitResponse>(reopened)).Status);
        Assert.Equal(PaymentStatus.Cancelled, (await table.GetAsync<PaymentResponse>($"/api/v1/payments/{second.Id}")).Status);
    }

    // 会計を始める前は払えない
    [Fact]
    public async Task PaymentRequiresCheckout()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0, adults: 1);

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = 100 });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VISIT_NOT_OPEN", await TestDevice.ReadErrorCodeAsync(response));
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    // 2 人で来店してサラダを頼み、会計を始める
    private async Task<(VisitResponse Visit, BillResponse Bill)> CheckoutAsync(TestStore store, TestDevice table)
    {
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        using var ordered = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Salad, 3)));
        ordered.EnsureSuccessStatusCode();
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });
        return (await TestDevice.ReadAsync<VisitResponse>(checkout), bill);
    }

    private static async Task<PaymentResponse> CreateQrAsync(TestDevice table, VisitResponse visit, decimal amount)
    {
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = amount });
        return await TestDevice.ReadAsync<PaymentResponse>(response);
    }

    // テーブルの決済端末で払う
    private static async Task<PaymentResponse> PayByCardAsync(TestDevice table, VisitResponse visit, decimal amount)
    {
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.CreditCard, Amount = amount });
        var payment = await TestDevice.ReadAsync<PaymentResponse>(created);
        using var result = await table.PostAsync($"/api/v1/payments/{payment.Id}/result", new PaymentResultRequest { Status = PaymentStatus.Completed, Provider = "card", ProviderReference = Guid.CreateVersion7().ToString("N") });
        return await TestDevice.ReadAsync<PaymentResponse>(result);
    }
}
