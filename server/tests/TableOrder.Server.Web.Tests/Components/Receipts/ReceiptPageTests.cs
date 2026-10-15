namespace TableOrder.Server.Web.Components.Receipts;

using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Orders;
using TableOrder.Contract.Payments;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Services;

// 電子レシートの画面 (お客様がテーブル端末のお礼の画面の QR から開く。サインインなし)
public sealed class ReceiptPageTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public ReceiptPageTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 払い終えた来店の電子レシートはサインインせずに開け、チェーン、テーブル、明細、税率ごとの対象額と税額、支払、事業者と登録番号を出す
    // 検索に載せず、ブラウザと途中の経路に残させない
    [Fact]
    public async Task ReceiptPageShowsPaidVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await SetIssuerAsync(store, "テストの事業者", "T0000000000002");
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        await OrderAsync(table, visit, TestMenu.Salad, TestMenu.Salad, TestMenu.Pizza);
        var (bill, url) = await PayAsync(table, visit);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("noindex", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
        var page = await TestAdmin.ReadPageAsync(response);
        Assert.Contains("電子レシート", page, StringComparison.Ordinal);
        Assert.Contains("チェーン", page, StringComparison.Ordinal);
        Assert.Contains("テーブル 1", page, StringComparison.Ordinal);
        Assert.All(bill.Lines, line => Assert.Contains(line.Name.Ja, page, StringComparison.Ordinal));
        Assert.Contains($"{Amount(bill.Lines[0].UnitPrice)} × {bill.Lines[0].Quantity}", page, StringComparison.Ordinal);
        Assert.Contains(Amount(bill.Total), page, StringComparison.Ordinal);
        var tax = Assert.Single(bill.Taxes);
        Assert.Contains($"{Rate(tax.Rate)} 対象", page, StringComparison.Ordinal);
        Assert.Contains(Amount(tax.TaxAmount), page, StringComparison.Ordinal);
        Assert.Contains("クレジットカード", page, StringComparison.Ordinal);
        Assert.Contains("テストの事業者", page, StringComparison.Ordinal);
        Assert.Contains("登録番号 T0000000000002", page, StringComparison.Ordinal);
    }

    // ブラウザの言語の重みの順に日本語と英語から選び、英語では品の名前もメニューの英語の名前にする
    [Fact]
    public async Task ReceiptPageUsesBrowserLanguage()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        await OrderAsync(table, visit, TestMenu.Salad);
        var (bill, url) = await PayAsync(table, visit);
        using var client = factory.CreateClient();

        // Act
        var english = await ReadPageAsync(client, url, "fr-FR, en-US;q=0.9, ja;q=0.8");
        var japanese = await ReadPageAsync(client, url, "en;q=0.5, ja-JP");

        // Assert
        var name = Assert.Single(bill.Lines).Name;
        Assert.Contains("E-receipt", english, StringComparison.Ordinal);
        Assert.Contains("Table 1", english, StringComparison.Ordinal);
        Assert.Contains(name.En!, english, StringComparison.Ordinal);
        Assert.DoesNotContain(name.Ja, english, StringComparison.Ordinal);
        Assert.Contains("電子レシート", japanese, StringComparison.Ordinal);
        Assert.Contains(name.Ja, japanese, StringComparison.Ordinal);
    }

    // 税率が 2 つ以上あるときは、明細に税率を添えて、税率ごとの対象額と税額を出す
    [Fact]
    public async Task ReceiptPageShowsEachTaxRate()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var lines = await OrderAsync(table, visit, TestMenu.Salad, TestMenu.Pizza);
        await factory.SetLineTaxRateAsync(store, lines[1].Id, 0.08m);
        var (bill, url) = await PayAsync(table, visit);
        using var client = factory.CreateClient();

        // Act
        var page = await ReadPageAsync(client, url, "ja");

        // Assert
        Assert.Equal(2, bill.Taxes.Count);
        Assert.All(bill.Lines, line => Assert.Contains($"{line.Name.Ja} ({Rate(line.TaxRate)})", page, StringComparison.Ordinal));
        Assert.All(bill.Taxes, tax =>
        {
            Assert.Contains($"{Rate(tax.Rate)} 対象", page, StringComparison.Ordinal);
            Assert.Contains(Amount(tax.TaxAmount), page, StringComparison.Ordinal);
        });
    }

    // 事業者は電子レシートを作ったときの店舗の設定を出し、あとで店舗の設定を替えても替えない (事業者のなかった店は、事業者のないレシートのまま)
    [Fact]
    public async Task ReceiptKeepsIssuerAtIssue()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        await OrderAsync(table, visit, TestMenu.Salad);
        var (_, url) = await PayAsync(table, visit);
        await SetIssuerAsync(store, "テストの事業者", "T0000000000002");
        using var client = factory.CreateClient();

        // Act
        var page = await ReadPageAsync(client, url, "ja");

        // Assert
        Assert.DoesNotContain("テストの事業者", page, StringComparison.Ordinal);
        Assert.DoesNotContain("T0000000000002", page, StringComparison.Ordinal);
    }

    // ないトークンは 404 にし、見つからないことだけを出す
    [Fact]
    public async Task ReceiptPageReturnsNotFoundForUnknownToken()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/receipts/" + new string('0', 32), UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("電子レシートが見つかりません", await TestAdmin.ReadPageAsync(response), StringComparison.Ordinal);
    }

    // 画面と同じ書式の額と税率 (例: ¥1,299、10%)
    private static string Amount(decimal value) => $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    private static string Rate(decimal rate) => $"{(rate * 100).ToString("0.#", CultureInfo.InvariantCulture)}%";

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    // 店舗の設定で事業者の名前と登録番号を入れる (管理画面と同じく、選んだ店舗の文脈で)
    private async Task SetIssuerAsync(TestStore store, string issuerName, string registrationNumber)
    {
        using var scope = factory.BeginStore(store);
        var service = factory.Services.GetRequiredService<SettingsService>();
        var settings = (await service.GetStoreSettingsAsync(TestContext.Current.CancellationToken))!;
        Assert.Null(await service.UpdateStoreSettingsAsync(settings with { IssuerName = issuerName, InvoiceRegistrationNumber = registrationNumber }, null, TestContext.Current.CancellationToken));
    }

    // 品を 1 つずつ頼む (税率を替えるテストのために、頼んだ明細を返す)
    private static async Task<OrderCreateRequestLine[]> OrderAsync(TestDevice table, VisitResponse visit, params Guid[] items)
    {
        var menu = await TestMenu.LoadAsync(table);
        var lines = items.Select(x => menu.Line(x)).ToArray();
        using var ordered = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(lines));
        ordered.EnsureSuccessStatusCode();
        return lines;
    }

    // 会計を始めてテーブルの決済端末で払い終え (来店が閉じて電子レシートができる)、電子レシートの URL の経路を返す
    private static async Task<(BillResponse Bill, Uri Url)> PayAsync(TestDevice table, VisitResponse visit)
    {
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = visit.Version });
        checkout.EnsureSuccessStatusCode();
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.CreditCard, Amount = bill.Total });
        var payment = await TestDevice.ReadAsync<PaymentResponse>(created);
        using var result = await table.PostAsync($"/api/v1/payments/{payment.Id}/result", new PaymentResultRequest { Status = PaymentStatus.Completed, Provider = "card", ProviderReference = Guid.CreateVersion7().ToString("N") });
        result.EnsureSuccessStatusCode();
        var receipt = await table.GetAsync<ReceiptResponse>($"/api/v1/visits/{visit.Id}/receipt");
        return (bill, new Uri(receipt.Url.AbsolutePath, UriKind.Relative));
    }

    // ブラウザの言語 (Accept-Language) を付けて画面を読む
    private static async Task<string> ReadPageAsync(HttpClient client, Uri url, string languages)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptLanguage.ParseAdd(languages);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TestAdmin.ReadPageAsync(response);
    }
}
