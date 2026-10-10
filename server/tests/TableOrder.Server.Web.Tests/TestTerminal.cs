namespace TableOrder.Server.Web;

using System.Threading.Channels;

using Microsoft.AspNetCore.Http.Connections;

using TableOrder.Client;
using TableOrder.Client.Rest;
using TableOrder.Client.SignalR;
using TableOrder.Contract.Devices;

// テストの端末の窓口。端末のアプリと同じ REST の窓口と SignalR の通知を、テストのサーバの中のハンドラと Long Polling でつなぐ
public sealed class TestTerminal : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Channel<OrderEvent> received = Channel.CreateUnbounded<OrderEvent>();

    private readonly Channel<DeviceDeniedEventArgs> denied = Channel.CreateUnbounded<DeviceDeniedEventArgs>();

    private readonly RestConnection connection;

    public TestDeviceContext Context { get; }

    public RestDeviceApi Device { get; }

    public RestTableApi Table { get; }

    public RestHallApi Hall { get; }

    public RestReceptionApi Reception { get; }

    public RestKitchenApi Kitchen { get; }

    public SignalROrderEvents Events { get; }

    private TestTerminal(ServerFactory factory)
    {
        var options = new OrderServerOptions
        {
            HandlerFactory = factory.Server.CreateHandler,
            HubTransports = HttpTransportType.LongPolling
        };
        Context = new TestDeviceContext(factory.Server.BaseAddress.ToString());
        connection = new RestConnection(Context, options, TimeProvider.System);
        Device = new RestDeviceApi(connection);
        Table = new RestTableApi(Context, connection);
        Hall = new RestHallApi(Context, connection);
        Reception = new RestReceptionApi(connection);
        Kitchen = new RestKitchenApi(Context, connection);
        Events = new SignalROrderEvents(Context, options, connection);
        Device.Denied += (_, e) => denied.Writer.TryWrite(e);
        Events.Received += (_, e) => received.Writer.TryWrite(e.Event);
    }

    public static TestTerminal Create(ServerFactory factory) => new(factory);

    public async ValueTask DisposeAsync()
    {
        await Events.DisposeAsync();
        connection.Dispose();
    }

    // 端末のアプリと同じく、鍵の公開鍵とペアリングコードで登録して、端末の id を設定に入れる (種類を省いたら、コードの種類で登録する)
    public Task<DevicePairResponse> PairAsync(string code, DeviceKind? kind = null) => PairAsync(code, null, kind ?? TestCodes.KindOf(code));

    // EMM で配った登録トークンで登録する
    public Task<DevicePairResponse> PairByTokenAsync(string enrollmentToken, DeviceKind kind) => PairAsync(null, enrollmentToken, kind);

    // この端末の鍵で送る登録の要求 (断られる登録を確かめる)
    public async Task<DevicePairRequest> CreatePairRequestAsync(string? code, string? enrollmentToken, DeviceKind kind) =>
        new()
        {
            PairingCode = code,
            EnrollmentToken = enrollmentToken,
            Kind = kind,
            PublicKey = DeviceCredentials.CreatePublicKey(await Context.Key.GetPublicKeyAsync()),
            DeviceName = "test"
        };

    private async Task<DevicePairResponse> PairAsync(string? code, string? enrollmentToken, DeviceKind kind)
    {
        var result = await Device.PairAsync(await CreatePairRequestAsync(code, enrollmentToken, kind), TestContext.Current.CancellationToken);
        Context.DeviceId = result.Content!.DeviceId;
        return result.Content;
    }

    // 次に届く通知
    public Task<OrderEvent> NextAsync() => ReadAsync(received.Reader);

    // 次に届く、端末が使えなくなった知らせ
    public Task<DeviceDeniedEventArgs> NextDeniedAsync() => ReadAsync(denied.Reader);

    private static async Task<T> ReadAsync<T>(ChannelReader<T> reader)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(Timeout);
        return await reader.ReadAsync(cts.Token);
    }
}
