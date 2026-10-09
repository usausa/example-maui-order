namespace TableOrder.Server.Web.Client.Rest;

using TableOrder.Client;
using TableOrder.Contract.Visits;

// 受付機のアプリの REST の窓口を、本物のサーバにつないで確かめる
public sealed class RestReceptionApiTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public RestReceptionApiTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 来店の開き方が受付機の店では、人数だけを送るとサーバが人数の入る空席 (定員の小さい順) を決めて来店を開く
    // 空席は開いた来店のテーブルが外れ、人数の入る空席がなければ満席で断られる。同じ id の送り直しは開いた来店を返す
    [Fact]
    public async Task ReceptionFlowThroughRest()
    {
        // Arrange
        var store = await factory.CreateStoreAsync(VisitOpening.Reception);
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.ReceptionCode);
        var api = terminal.Reception;
        var cancel = TestContext.Current.CancellationToken;

        // Act / Assert: 起動で読むもの (店舗、空席)
        Assert.Equal(store.StoreId, (await api.GetStoreAsync(cancel)).Content!.Id);
        Assert.Equal(store.TableIds, (await api.GetTablesAsync(TableStatus.Vacant, cancel)).Content!.Items.Select(static x => x.Id));

        // Act / Assert: 2 人は定員 4 の先のテーブル、5 人は定員 6 のテーブルに決まる
        var pair = new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 1, Children = 1 };
        var two = (await api.OpenVisitAsync(pair, cancel)).Content!;
        Assert.Equal((store.TableIds[0], VisitOpenedBy.Reception), (two.TableId, two.OpenedBy));
        var five = (await api.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 5 }, cancel)).Content!;
        Assert.Equal(store.TableIds[2], five.TableId);
        Assert.Equal([store.TableIds[1]], (await api.GetTablesAsync(TableStatus.Vacant, cancel)).Content!.Items.Select(static x => x.Id));

        // Act / Assert: 人数の入る空席がなければ断られ、送り直しは開いた来店を返す
        var full = await api.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 5 }, cancel);
        Assert.Equal(ApiStatus.Rejected, full.Status);
        Assert.Equal("NO_VACANT_TABLE", full.ErrorCode);
        Assert.Equal(two.Id, (await api.OpenVisitAsync(pair, cancel)).Content!.Id);
    }

    // 来店の開き方が受付機でない店では、受付機は来店を開けない
    [Fact]
    public async Task OpeningIsRejectedWhenNotReception()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.ReceptionCode);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        var rejected = await terminal.Reception.OpenVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 2 }, cancel);

        // Assert
        Assert.Equal(ApiStatus.Rejected, rejected.Status);
        Assert.Equal("VISIT_OPENING_DISABLED", rejected.ErrorCode);
    }
}
