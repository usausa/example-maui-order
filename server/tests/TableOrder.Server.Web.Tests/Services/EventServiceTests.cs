namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Events;
using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Services;

// 店舗の通知 (管理画面の通知の記録)
public sealed class EventServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public EventServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private EventService Events => factory.Services.GetRequiredService<EventService>();

    // 直近の通知を、新しい順に決めた数まで返す
    [Fact]
    public async Task RecentReturnsLatestEventsNewestFirst()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await factory.OpenVisitAsync(store, 0);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var paused = await hall.PutAsync("/api/v1/store/ordering", new StoreOrderingRequest { Paused = true });
        paused.EnsureSuccessStatusCode();

        // Act
        List<EventEntity> latest;
        List<EventEntity> all;
        using (factory.BeginStore(store))
        {
            latest = await Events.GetRecentAsync(1, TestContext.Current.CancellationToken);
            all = await Events.GetRecentAsync(20, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(EventTypes.StoreUpdated, Assert.Single(latest).Type);
        Assert.Equal([EventTypes.StoreUpdated, EventTypes.VisitOpened], all.Select(static x => x.Type));
        Assert.True(all[0].Seq > all[1].Seq);
    }
}
