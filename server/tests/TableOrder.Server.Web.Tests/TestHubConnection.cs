namespace TableOrder.Server.Web;

using System.Text.Json;
using System.Threading.Channels;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

using TableOrder.Contract.Events;

// テストの端末の通知のハブの接続。届いた通知を届いた順に読む (テストのサーバは WebSocket を通さないので Long Polling でつなぐ)
public sealed class TestHubConnection : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HubConnection connection;

    private readonly Channel<EventListResponseItem> events = Channel.CreateUnbounded<EventListResponseItem>();

    private readonly TaskCompletionSource<long> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TestHubConnection(HubConnection connection)
    {
        this.connection = connection;
        connection.On<EventListResponseItem>(HubMethods.Event, x => events.Writer.TryWrite(x));
        connection.On<long>(HubMethods.Ready, OnReady);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    // つないで、サーバが端末をグループに入れ終えるまで待つ
    public static async Task<TestHubConnection> ConnectAsync(ServerFactory factory, TestDevice device)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/store"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(device.AccessToken);
            })
            .Build();
        var hub = new TestHubConnection(connection);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
            await hub.ready.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
        catch
        {
            await hub.DisposeAsync();
            throw;
        }

        return hub;
    }

    // 次に届く通知。端末と同じく、ready で受けた番号までの通知はつなぐ前の状態に入っているので読み飛ばす (送り手の遅れで、つないだあとに届くことがある)
    public async Task<EventListResponseItem> NextAsync()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(Timeout);
        var after = await ready.Task;
        while (true)
        {
            var item = await events.Reader.ReadAsync(cts.Token);
            if (item.Seq > after)
            {
                return item;
            }
        }
    }

    // サーバが接続を切るまで待つ (テストの接続はつなぎ直さない)
    public Task WaitClosedAsync() => closed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

    private void OnReady(long lastSeq) => ready.TrySetResult(lastSeq);

    // 中身を型で読む
    public static T Read<T>(EventListResponseItem item) =>
        item.Data.Deserialize<T>(TestDevice.JsonOptions)!;
}
