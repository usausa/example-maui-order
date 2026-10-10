namespace TableOrder.Server.Web.Hubs;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Events;
using TableOrder.Server.Core.Services;

public sealed class EventDispatcherTests
{
    // ほかの店舗の知らせが見回りの間隔より短く続いても、間隔ごとに見回って、ほかのサーバが書いた通知を送る
    [Fact]
    public async Task SweepRunsWhileOtherStoresSignal()
    {
        // Arrange: 見回りの間隔を 1 秒にしたサーバと、知らせの続く店舗
        await using var server = new ServerFactory();
        server.Settings["Event:SweepSeconds"] = "1";
        var store = await server.CreateStoreAsync();
        var busy = await server.CreateStoreAsync();
        using var hall = new TestDevice(server.CreateClient());
        await hall.SignInAsync(store.HallCode);
        await using var connection = await TestHubConnection.ConnectAsync(server, hall);
        using var signaling = new CancellationTokenSource();
        var signals = KeepSignalingAsync(server.Services.GetRequiredService<IEventPublisher>(), busy, signaling.Token);

        // Act
        await server.WriteEventElsewhereAsync(store, EventTypes.StoreUpdated);
        var received = await connection.NextAsync();
        await signaling.CancelAsync();
        await signals;

        // Assert
        Assert.Equal(EventTypes.StoreUpdated, received.Type);
    }

    // 店舗の知らせを、止めるまで 100 ミリ秒ごとに送り続ける
    private static async Task KeepSignalingAsync(IEventPublisher publisher, TestStore store, CancellationToken token)
    {
        while (true)
        {
            publisher.Publish(store.TenantId, store.StoreId);
            try
            {
                await Task.Delay(100, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
