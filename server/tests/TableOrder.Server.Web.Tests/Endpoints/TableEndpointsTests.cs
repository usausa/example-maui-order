namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;

public sealed class TableEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public TableEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // テーブルを表示順に、今の来店の要約と一緒に返す
    [Fact]
    public async Task TablesHaveVisitSummary()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var opened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[1], Adults = 2, Children = 1 });
        var visit = await TestDevice.ReadAsync<VisitResponse>(opened);

        // Act
        var tables = await hall.GetAsync<TableListResponse>("/api/v1/tables");

        // Assert
        Assert.Equal(store.TableIds, tables.Items.Select(static x => x.Id));
        Assert.Null(tables.Items[0].Visit);
        var summary = tables.Items[1].Visit!;
        Assert.Equal(visit.Id, summary.VisitId);
        Assert.Equal(2, summary.Adults);
        Assert.Equal(1, summary.Children);
        Assert.Equal(VisitStatus.Open, summary.Status);
        Assert.Equal(0, summary.UnservedCount);
        Assert.Equal(0, summary.OpenCallCount);
        Assert.Equal(visit.Version, summary.Version);
    }

    // 受付機は空いているテーブルだけを読む
    [Fact]
    public async Task ReceptionReadsVacantTables()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var reception = new TestDevice(factory.CreateClient());
        await reception.SignInAsync(store.ReceptionCode);
        using var opened = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        opened.EnsureSuccessStatusCode();

        // Act
        var vacant = await reception.GetAsync<TableListResponse>("/api/v1/tables?status=Vacant");
        var occupied = await reception.GetAsync<TableListResponse>("/api/v1/tables?status=Occupied");

        // Assert
        Assert.Equal(store.TableIds.Skip(1), vacant.Items.Select(static x => x.Id));
        Assert.Equal([store.TableIds[0]], occupied.Items.Select(static x => x.Id));
    }

    // 知らない状態は受けない
    [Fact]
    public async Task UnknownStatusIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        using var response = await hall.Client.GetAsync(new Uri("/api/v1/tables?status=Unknown", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // テーブル端末はテーブルの一覧を読めない
    [Fact]
    public async Task TableDeviceCannotReadTables()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[0]);

        // Act
        using var response = await table.Client.GetAsync(new Uri("/api/v1/tables", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }
}
