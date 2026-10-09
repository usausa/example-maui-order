namespace TableOrder.Client.SignalR;

using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Client.Rest;
using TableOrder.Contract.Events;

// 注文サーバの通知 (SignalR のハブ)。ハブは端末をグループに入れ終えたら ready で店舗の今の通し番号を送る
// はじめてつないだときはその番号から数え (このあとに端末が今の状態を読む)、つなぎ直したときは抜けた通知を読んでから続ける
// つなぎ直しの間 (切れてから抜けた通知を読み終えるまで) に届いた通知はためておき、抜けた通知と合わせて seq の順に渡す (EventSequencer)
// 追いかけられないときは Expired で知らせる。ConnectAsync で作り直したら、前の接続の通知と読み込みは渡さない
// 切れたら間をおいてつなぎ直し続ける (アクセストークンと抜けた通知は REST の送り方で受け取り、つなぎ直しが 401 で断られたらトークンを取り直す)
public sealed class SignalROrderEvents : IOrderEvents, IAsyncDisposable
{
    private const string HubPath = "hubs/store";

    // つないでから ready を待つ時間
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);

    // つなぐ前の問い合わせ (negotiate) を待つ時間。サーバに届かないと SignalR の既定 (120 秒) まで待ち、その間つなぎ直しが止まる
    private static readonly TimeSpan NegotiateTimeout = TimeSpan.FromSeconds(15);

    // つなぎ直す間隔 (はじめはすぐに、そのあとは延ばして 30 秒ごと)
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly IDeviceContext context;

    private readonly OrderServerOptions options;

    private readonly RestConnection rest;

    private readonly SemaphoreSlim connectLock = new(1, 1);

    // 渡す通知 (Event が null は追いかけられなくなった知らせ)。1 つの流れで出し、seq の順を崩さない
    private readonly Channel<Dispatch> dispatching = Channel.CreateUnbounded<Dispatch>(new UnboundedChannelOptions { SingleReader = true });

    private readonly EventSequencer sequencer;

    private volatile HubConnection? connection;

    // ハブに渡した最後のトークンと、ハブが断ったトークン (サーバの署名の鍵が替わったときなど。次につなぐときは取り直す)
    private volatile string? providedToken;

    private volatile string? rejectedToken;

    public event EventHandler<OrderEventArgs>? Received;

    public event EventHandler? Expired;

    public SignalROrderEvents(IDeviceContext context, OrderServerOptions options, RestConnection rest)
    {
        this.context = context;
        this.options = options;
        this.rest = rest;
        sequencer = new EventSequencer(Emit);
        _ = DispatchAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await connectLock.WaitAsync();
        try
        {
            await StopAsync();
        }
        finally
        {
            connectLock.Release();
        }

        dispatching.Writer.TryComplete();
        connectLock.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Connection
    //--------------------------------------------------------------------------------

    // つないで ready を待つ。つないでいても、つなぎ直して数え始めの位置を決め直す (起動で今の状態を読み直すので)
    public async ValueTask<ApiResult<NoContent>> ConnectAsync(CancellationToken cancel = default)
    {
        try
        {
            await connectLock.WaitAsync(cancel);
        }
        catch (OperationCanceledException ex)
        {
            return ApiResult.Failure<NoContent>(ApiStatus.Canceled, exception: ex);
        }

        try
        {
            await StopAsync();
            if (!RestConnection.TryCreateUri(context.ApiEndPoint, HubPath, out var uri))
            {
                return ApiResult.Failure<NoContent>(ApiStatus.Unavailable);
            }

            // トークンが取れなければつながない (登録していない、無効にされた端末)
            var access = await rest.GetAccessTokenAsync(null, false, cancel);
            if (!access.IsSuccess)
            {
                return RestConnection.Failure<string, NoContent>(access);
            }

            var started = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            var hub = Build(uri, sequencer.Begin(), started);
            connection = hub;
            await hub.StartAsync(cancel);
            await started.Task.WaitAsync(ReadyTimeout, cancel);
            return ApiResult.Success(NoContent.Value);
        }
        catch (OperationCanceledException ex) when (cancel.IsCancellationRequested)
        {
            await StopAsync();
            return ApiResult.Failure<NoContent>(ApiStatus.Canceled, exception: ex);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            await StopAsync();
            var status = ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } ? ApiStatus.Unauthorized : ApiStatus.Unavailable;
            return ApiResult.Failure<NoContent>(status, exception: ex);
        }
        finally
        {
            connectLock.Release();
        }
    }

    // id は数え方の接続 (EventSequencer.Begin)。この接続の ready と通知だけを数え、作り直したあとに届いたものは使わない
    private HubConnection Build(Uri uri, long id, TaskCompletionSource<long> started)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(uri, http =>
            {
                http.Transports = options.HubTransports;
                http.HttpMessageHandlerFactory = handler => new NegotiateTimeoutHandler(options.HandlerFactory?.Invoke() ?? handler);
                http.AccessTokenProvider = ProvideTokenAsync;
            })
            .WithAutomaticReconnect(new RetryPolicy(RejectIfUnauthorized))
            .AddJsonProtocol(static json => json.PayloadSerializerOptions = ClientJsonContext.Default.Options)
            .Build();
        hub.On<EventListResponseItem>(HubMethods.Event, item => sequencer.Receive(id, item));
        hub.On<long>(HubMethods.Ready, seq => OnReady(id, started, seq));
        hub.Reconnecting += _ =>
        {
            sequencer.Interrupt(id);
            return Task.CompletedTask;
        };
        hub.Closed += _ => RestartAsync(hub, id);
        return hub;
    }

    private ValueTask StopAsync()
    {
        var current = connection;
        connection = null;
        return current?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    // つなぎ直しをあきらめて閉じたとき (サーバが切った) は、間をおいて始め直す (ready を受けたら抜けた通知を読む)
    private async Task RestartAsync(HubConnection hub, long id)
    {
        sequencer.Interrupt(id);
        for (var attempt = 0; connection == hub; attempt++)
        {
            await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)]);
            if (connection != hub)
            {
                return;
            }

            try
            {
                await hub.StartAsync();
                return;
            }
            catch (Exception ex) when (IsConnectionFailure(ex))
            {
                // 次の間隔でつなぎ直す
                RejectIfUnauthorized(ex);
            }
        }
    }

    private async Task<string?> ProvideTokenAsync()
    {
        var token = (await rest.GetAccessTokenAsync(rejectedToken, false, CancellationToken.None)).Content;
        providedToken = token;
        return token;
    }

    // つなぎ直しが 401 で断られたら、渡したトークンを次は使わない
    private void RejectIfUnauthorized(Exception? ex)
    {
        if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
        {
            rejectedToken = providedToken;
        }
    }

    private static bool IsConnectionFailure(Exception ex) =>
        ex is HttpRequestException or WebSocketException or IOException or TimeoutException or OperationCanceledException or InvalidOperationException or InvalidDataException;

    //--------------------------------------------------------------------------------
    // Receive
    //--------------------------------------------------------------------------------

    // はじめは数え始めの位置にし (つなぐのを待っている ConnectAsync に知らせる)、つなぎ直したときは抜けた通知を読む
    private void OnReady(long id, TaskCompletionSource<long> started, long seq)
    {
        if (sequencer.Ready(id, seq) is { } request)
        {
            _ = CatchUpAsync(request);
        }
        else
        {
            started.TrySetResult(seq);
        }
    }

    // 抜けた通知を読み、ためた通知と合わせて seq の順に渡す。読めなければ (例外も含む) 今の番号から数え直し、読み直すように知らせる
    private async Task CatchUpAsync(CatchUpRequest request)
    {
        EventListResponse? content = null;
        try
        {
            content = (await rest.GetAsync(String.Create(CultureInfo.InvariantCulture, $"events?after={request.After}"), ClientJsonContext.Default.EventListResponse, CancellationToken.None)).Content;
        }
        finally
        {
            sequencer.Complete(request, content);
        }
    }

    // 数えた通知を渡す流れに入れる。扱わない種類と読めない中身は渡さない (番号は数え方が進めている)
    private void Emit(long id, EventListResponseItem? item)
    {
        if (item is null)
        {
            dispatching.Writer.TryWrite(new Dispatch(id, null));
        }
        else if (Map(item) is { } e)
        {
            dispatching.Writer.TryWrite(new Dispatch(id, e));
        }
    }

    // 作り直す前の接続の通知と知らせは渡さない (つなぎ直しで数え直した受け手に、前の接続 (ほかの店舗のこともある) の通知を渡さない)
    private async Task DispatchAsync()
    {
        await foreach (var item in dispatching.Reader.ReadAllAsync())
        {
            if (item.Connection != sequencer.Connection)
            {
                continue;
            }

            if (item.Event is null)
            {
                Expired?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Received?.Invoke(this, new OrderEventArgs(item.Event, item.Connection));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Map
    //--------------------------------------------------------------------------------

    // 端末の扱う通知の形にする (扱わない種類と、読めない中身は渡さない)
    private static OrderEvent? Map(EventListResponseItem item)
    {
        try
        {
            return item.Type switch
            {
                EventTypes.VisitOpened => new VisitOpenedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.VisitResponse)),
                EventTypes.VisitUpdated => new VisitUpdatedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.VisitResponse)),
                EventTypes.VisitMoved => new VisitMovedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.VisitMovedEventData).Visit),
                EventTypes.VisitClosed => new VisitClosedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.VisitResponse)),
                EventTypes.StoreUpdated => new StoreUpdatedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.StoreResponse)),
                EventTypes.StockUpdated => new StockUpdatedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.StockUpdatedEventData).Items),
                EventTypes.OrderCreated => new OrderCreatedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.OrderListResponseItem)),
                EventTypes.OrderLinesUpdated => LinesUpdated(item, Read(item, ClientJsonContext.Default.OrderLinesUpdatedEventData)),
                EventTypes.CallCreated => new CallCreatedEvent(item.Seq, item.OccurredAt),
                EventTypes.CallUpdated => new CallUpdatedEvent(item.Seq, item.OccurredAt),
                EventTypes.TicketCreated => new TicketCreatedEvent(item.Seq, item.OccurredAt),
                EventTypes.TicketUpdated => new TicketUpdatedEvent(item.Seq, item.OccurredAt),
                EventTypes.DeviceUpdated => new DeviceUpdatedEvent(item.Seq, item.OccurredAt, Read(item, ClientJsonContext.Default.DeviceUpdatedEventData).DeviceId),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OrderLinesUpdatedEvent LinesUpdated(EventListResponseItem item, OrderLinesUpdatedEventData data) =>
        new(item.Seq, item.OccurredAt, data.VisitId, data.Orders);

    private static T Read<T>(EventListResponseItem item, JsonTypeInfo<T> type) =>
        item.Data.Deserialize(type) ?? throw new JsonException("The event has no data.");

    // 渡す通知と、数えた接続 (Event が null は追いかけられなくなった知らせ)
    private sealed record Dispatch(long Connection, OrderEvent? Event);

    // つなぐ前の問い合わせだけに時間を区切る (Long Polling の待ちは区切らない)
    private sealed class NegotiateTimeoutHandler : DelegatingHandler
    {
        public NegotiateTimeoutHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/negotiate", StringComparison.Ordinal) != true)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(NegotiateTimeout);
            return await base.SendAsync(request, timeout.Token);
        }
    }

    // 店の回線が戻るまでつなぎ直し続ける。つなぎ直せなかった理由はトークンを取り直すかの判断に渡す
    private sealed class RetryPolicy : IRetryPolicy
    {
        private readonly Action<Exception?> failed;

        public RetryPolicy(Action<Exception?> failed)
        {
            this.failed = failed;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            failed(retryContext.RetryReason);
            return RetryDelays[(int)Math.Min(retryContext.PreviousRetryCount, RetryDelays.Length - 1)];
        }
    }
}
