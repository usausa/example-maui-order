namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Bills;
using TableOrder.Contract.Calls;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Payments;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Services;

// 古いデータの片付け (サーバの中の CleanupService を、裏の処理と同じく店舗の文脈で呼ぶ)
public sealed class CleanupServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public CleanupServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private CleanupService Cleanup => factory.Services.GetRequiredService<CleanupService>();

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // 営業日から残す日数を過ぎた閉じた来店 (払い終えた、取りやめた) は、確認の記録・注文・明細とオプション・チケット・呼び出し・支払・電子レシートと一緒に消える
    // 残す日数ちょうどの来店、開いている来店、ほかの店舗の来店は残る
    [Fact]
    public async Task ClosedVisitsBeyondRetentionAreDeleted()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var other = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var hall = await SignInAsync(store.HallCode);
        using var otherHall = await SignInAsync(other.HallCode);
        var paid = await PayVisitAsync(store, table);
        var cancelled = await CancelVisitAsync(hall, store.TableIds[1]);
        var boundary = await CancelVisitAsync(hall, store.TableIds[0]);
        var open = await OpenAsync(hall, store.TableIds[2]);
        var otherCancelled = await CancelVisitAsync(otherHall, other.TableIds[0]);
        await factory.MoveVisitToPreviousDayAsync(store, paid, 91);
        await factory.MoveVisitToPreviousDayAsync(store, cancelled, 120);
        await factory.MoveVisitToPreviousDayAsync(store, boundary, 90);
        await factory.MoveVisitToPreviousDayAsync(store, open.Id, 200);
        await factory.MoveVisitToPreviousDayAsync(other, otherCancelled, 120);
        Assert.All(await factory.CountVisitRowsAsync(store, paid), static x => Assert.True(x.Value > 0, x.Key));

        // Act
        int deleted;
        using (factory.BeginStore(store))
        {
            deleted = await Cleanup.DeleteClosedVisitsAsync(90, 200, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(2, deleted);
        Assert.All(await factory.CountVisitRowsAsync(store, paid), static x => Assert.Equal(0, x.Value));
        Assert.Equal(0, (await factory.CountVisitRowsAsync(store, cancelled))["Visits"]);
        Assert.Equal(1, (await factory.CountVisitRowsAsync(store, boundary))["Visits"]);
        Assert.Equal(1, (await factory.CountVisitRowsAsync(store, open.Id))["Visits"]);
        Assert.Equal(1, (await factory.CountVisitRowsAsync(other, otherCancelled))["Visits"]);
    }

    // 1 回に消す数を超える来店も、トランザクションを分けてすべて消す
    [Fact]
    public async Task ClosedVisitsAreDeletedInBatches()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visits = new List<Guid>();
        foreach (var tableId in store.TableIds)
        {
            var visit = await CancelVisitAsync(hall, tableId);
            await factory.MoveVisitToPreviousDayAsync(store, visit, 100);
            visits.Add(visit);
        }

        // Act
        int deleted;
        using (factory.BeginStore(store))
        {
            deleted = await Cleanup.DeleteClosedVisitsAsync(90, 2, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(visits.Count, deleted);
        foreach (var visit in visits)
        {
            Assert.Equal(0, (await factory.CountVisitRowsAsync(store, visit))["Visits"]);
        }
    }

    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    // メニューの公開は、今のメニューと新しい順に残す数だけを残して消す (今のメニューは古くても残し、読める)
    [Fact]
    public async Task MenuPublicationsKeepCurrentAndRecent()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var first = Assert.Single(await factory.QueryMenuPublicationIdsAsync(store));
        await factory.PublishMenuAsync(store, static _ => { });
        await factory.PublishMenuAsync(store, static _ => { });
        var latest = await factory.PublishMenuAsync(store, static _ => { });
        await factory.UseMenuPublicationAsync(store, first);

        // Act
        int deleted;
        using (factory.BeginStore(store))
        {
            deleted = await Cleanup.DeleteMenuPublicationsAsync(1, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(2, deleted);
        var remaining = await factory.QueryMenuPublicationIdsAsync(store);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(first, remaining);
        Assert.Contains(latest, remaining);
        using var table = await SignInAsync(store.TableCodes[0]);
        Assert.NotEmpty((await table.GetAsync<MenuResponse>("/api/v1/menu")).Items);
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

    private static async Task<VisitResponse> OpenAsync(TestDevice hall, Guid tableId)
    {
        using var response = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = tableId, Adults = 2 });
        return await TestDevice.ReadAsync<VisitResponse>(response);
    }

    // 来店を開いて、注文のないまま取りやめる
    private static async Task<Guid> CancelVisitAsync(TestDevice hall, Guid tableId)
    {
        var visit = await OpenAsync(hall, tableId);
        using var cancelled = await hall.PostAsync($"/api/v1/visits/{visit.Id}/cancel", new VisitCancelRequest { Version = visit.Version });
        cancelled.EnsureSuccessStatusCode();
        return visit.Id;
    }

    // テーブル 1 で来店を開き、お酒の確認に答えてオプションつきの品とお酒を頼み、店員を呼んで、テーブルで払い終える (支払と電子レシートが残る)
    private async Task<Guid> PayVisitAsync(TestStore store, TestDevice table)
    {
        var visit = await factory.OpenVisitAsync(store, 0);
        var menu = await TestMenu.LoadAsync(table);
        using var confirmed = await table.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = TestMenu.AlcoholRuleId });
        confirmed.EnsureSuccessStatusCode();
        using var ordered = await table.PostAsync($"/api/v1/visits/{visit.Id}/orders", menu.Order(menu.Line(TestMenu.Hamburg, 1, OrderTiming.Now, TestMenu.CheeseSauce), menu.Line(TestMenu.Beer)));
        ordered.EnsureSuccessStatusCode();
        using var called = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Staff" });
        called.EnsureSuccessStatusCode();

        var current = await table.GetAsync<VisitResponse>("/api/v1/devices/me/visit");
        var bill = await table.GetAsync<BillResponse>($"/api/v1/visits/{visit.Id}/bill");
        using var checkout = await table.PostAsync($"/api/v1/visits/{visit.Id}/checkout", new CheckoutRequest { BillVersion = bill.BillVersion, Version = current.Version });
        checkout.EnsureSuccessStatusCode();
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/payments", new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.CreditCard, Amount = bill.Total });
        var payment = await TestDevice.ReadAsync<PaymentResponse>(created);
        using var result = await table.PostAsync($"/api/v1/payments/{payment.Id}/result", new PaymentResultRequest { Status = PaymentStatus.Completed, Provider = "card", ProviderReference = Guid.CreateVersion7().ToString("N") });
        result.EnsureSuccessStatusCode();
        return visit.Id;
    }
}
