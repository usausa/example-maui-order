namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Events;
using TableOrder.Contract.Stores;
using TableOrder.Contract.Visits;

public sealed class EventEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public EventEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 抜けた通知を通し番号の順に返し、店舗の今の通し番号を付ける
    [Fact]
    public async Task HallCatchesUpMissedEvents()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        using var updated = await hall.PatchAsync($"/api/v1/visits/{visit.Id}", new VisitUpdateRequest { Adults = 4, Version = visit.Version });
        updated.EnsureSuccessStatusCode();

        // Act
        var events = await hall.GetAsync<EventListResponse>("/api/v1/events?after=0");
        var rest = await hall.GetAsync<EventListResponse>("/api/v1/events?after=1");
        var none = await hall.GetAsync<EventListResponse>("/api/v1/events?after=2");

        // Assert
        Assert.Equal(2, events.LastSeq);
        Assert.Equal([1L, 2L], events.Items.Select(static x => x.Seq));
        Assert.Equal([EventTypes.VisitOpened, EventTypes.VisitUpdated], events.Items.Select(static x => x.Type));
        Assert.Equal(4, TestHubConnection.Read<VisitResponse>(events.Items[1]).Adults);
        Assert.Equal([2L], rest.Items.Select(static x => x.Seq));
        Assert.Empty(none.Items);
    }

    // テーブル端末は自分のテーブルの通知と店舗の通知だけを受け、キッチン端末は来店の通知を受けない
    [Fact]
    public async Task EventsAreFilteredByDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[1]);
        using var kitchen = await SignInAsync(store.KitchenCode);
        await OpenAsync(hall, store.TableIds[0]);
        var own = await OpenAsync(hall, store.TableIds[1]);
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Act
        var hallEvents = await hall.GetAsync<EventListResponse>("/api/v1/events?after=0");
        var tableEvents = await table.GetAsync<EventListResponse>("/api/v1/events?after=0");
        var kitchenEvents = await kitchen.GetAsync<EventListResponse>("/api/v1/events?after=0");

        // Assert
        Assert.Equal([1L, 2L, 3L], hallEvents.Items.Select(static x => x.Seq));
        Assert.Equal([EventTypes.VisitOpened, EventTypes.StoreUpdated], tableEvents.Items.Select(static x => x.Type));
        Assert.Equal(own.Id, TestHubConnection.Read<VisitResponse>(tableEvents.Items[0]).Id);
        Assert.Equal([EventTypes.StoreUpdated], kitchenEvents.Items.Select(static x => x.Type));
        Assert.Equal(3, tableEvents.LastSeq);
    }

    // 店舗の通し番号より先は追いかけられない (端末は今の状態を読み直す)
    [Fact]
    public async Task AfterBeyondLastSeqIsExpired()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);

        // Act
        using var response = await hall.Client.GetAsync(new Uri("/api/v1/events?after=5", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("EVENTS_EXPIRED", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 最後に受けた通し番号は必ず送る
    [Fact]
    public async Task MissingAfterIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);

        // Act
        using var response = await hall.Client.GetAsync(new Uri("/api/v1/events", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    private static async Task<VisitResponse> OpenAsync(TestDevice device, Guid tableId)
    {
        using var response = await device.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = tableId, Adults = 2 });
        return await TestDevice.ReadAsync<VisitResponse>(response);
    }
}
