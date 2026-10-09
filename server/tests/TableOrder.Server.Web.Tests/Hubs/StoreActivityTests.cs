namespace TableOrder.Server.Web.Hubs;

using Microsoft.Extensions.DependencyInjection;

// 店舗の通知を送ったことを、店舗を見ている画面に知らせる
public sealed class StoreActivityTests : IClassFixture<ServerFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ServerFactory factory;

    public StoreActivityTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 送り手が店舗の通知を送ると、その店舗を見ている画面に知らせる
    [Fact]
    public async Task DispatcherNotifiesStoreWatchers()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var activity = factory.Services.GetRequiredService<StoreActivity>();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = activity.Watch(store.TenantId, store.StoreId, () => changed.TrySetResult());

        // Act
        await factory.OpenVisitAsync(store, 0);

        // Assert
        await changed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    // 知らせるのはその店舗を見ているものだけで、見るのをやめたら知らせない
    [Fact]
    public void NotifyCallsOnlyWatchersOfStore()
    {
        // Arrange
        var activity = new StoreActivity();
        var tenantId = Guid.CreateVersion7();
        var storeId = Guid.CreateVersion7();
        var watched = 0;
        var other = 0;
        using var otherWatcher = activity.Watch(tenantId, Guid.CreateVersion7(), () => other++);

        // Act
        using (activity.Watch(tenantId, storeId, () => watched++))
        {
            activity.Notify(tenantId, storeId);
        }

        activity.Notify(tenantId, storeId);

        // Assert
        Assert.Equal(1, watched);
        Assert.Equal(0, other);
    }
}
