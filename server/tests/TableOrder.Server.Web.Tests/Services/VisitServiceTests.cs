namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Client;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Services;

// 管理画面の案内 (サーバの中の VisitService を、管理画面と同じく選んだ店舗の文脈で呼ぶ)
public sealed class VisitServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public VisitServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private VisitService Visits => factory.Services.GetRequiredService<VisitService>();

    private StoreService Stores => factory.Services.GetRequiredService<StoreService>();

    // 案内で開いた来店はスタッフが開いたものになり、そのテーブルの端末に届いて、テーブルの一覧では来店中になる
    [Fact]
    public async Task GuideOpensVisitAndNotifiesTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[1]);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);

        // Act
        VisitResponse visit;
        TableListResponse tables;
        using (factory.BeginStore(store))
        {
            visit = (await Visits.CreateAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[1], Adults = 2, Children = 1 }, cancel)).Value!;
            tables = (await Stores.GetTablesAsync(null, cancel)).Value!;
        }

        // Assert
        Assert.Equal(VisitOpenedBy.Hall, visit.OpenedBy);
        Assert.Equal(visit.Id, Assert.IsType<VisitOpenedEvent>(await terminal.NextAsync()).Visit.Id);
        var opened = Assert.Single(tables.Items, x => x.Visit is not null).Visit!;
        Assert.Equal(visit.Id, opened.VisitId);
        Assert.Equal((2, 1), (opened.Adults, opened.Children));
        Assert.Equal(VisitStatus.Open, opened.Status);
    }

    // レジで払って閉じた来店と、取りやめた来店は、そのテーブルの端末に届いてテーブルが空く
    [Fact]
    public async Task GuideClosesAndCancelsVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var cancel = TestContext.Current.CancellationToken;
        await using var terminal = TestTerminal.Create(factory);
        await terminal.PairAsync(store.TableCodes[0]);
        var first = await factory.OpenVisitAsync(store, 0);
        Assert.True((await terminal.Events.ConnectAsync(cancel)).IsSuccess);

        // Act / Assert: レジで払った来店を閉じる
        using (factory.BeginStore(store))
        {
            Assert.True((await Visits.CloseAsync(first.Id, new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = first.Version }, cancel)).Succeeded);
        }

        var closed = Assert.IsType<VisitClosedEvent>(await terminal.NextAsync()).Visit;
        Assert.Equal((VisitStatus.Closed, VisitClosedBy.Register), (closed.Status, closed.ClosedBy));

        // Act / Assert: 注文のないまま帰った来店を取りやめる
        using (factory.BeginStore(store))
        {
            var second = (await Visits.CreateAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 1 }, cancel)).Value!;
            Assert.True((await Visits.CancelAsync(second.Id, new VisitCancelRequest { Version = second.Version }, cancel)).Succeeded);
        }

        Assert.IsType<VisitOpenedEvent>(await terminal.NextAsync());
        Assert.Equal(VisitStatus.Cancelled, Assert.IsType<VisitClosedEvent>(await terminal.NextAsync()).Visit.Status);
        using (factory.BeginStore(store))
        {
            Assert.All((await Stores.GetTablesAsync(null, cancel)).Value!.Items, static x => Assert.Null(x.Visit));
        }
    }
}
