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
// 抜けた通知を読む間に届いた通知はためておき、抜けた通知のあとに seq の順に渡す。追いかけられないときは Expired で知らせる
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

    private readonly Lock sync = new();

    private readonly SemaphoreSlim connectLock = new(1, 1);

    // 渡す通知 (null は追いかけられなくなった知らせ)。1 つの流れで出し、seq の順を崩さない
    private readonly Channel<OrderEvent?> dispatching = Channel.CreateUnbounded<OrderEvent?>(new UnboundedChannelOptions { SingleReader = true });

    // 数え始めの位置を決める前と、抜けた通知を読む間に届いた通知
    private readonly List<EventListResponseItem> buffered = [];

    private volatile HubConnection? connection;

    // ハブに渡した最後のトークンと、ハブが断ったトークン (サーバの署名の鍵が替わったときなど。次につなぐときは取り直す)
    private volatile string? providedToken;

    private volatile string? rejectedToken;

    private TaskCompletionSource<long>? ready;

    // 数え始めの位置を決めた (ready を受けた)
    private bool counting;

    private bool catchingUp;

    // 渡した (読み飛ばしたものも含めて見た) 最後の通し番号
    private long lastSeq;

    public event EventHandler<OrderEventArgs>? Received;

    public event EventHandler? Expired;

    public SignalROrderEvents(IDeviceContext context, OrderServerOptions options, RestConnection rest)
    {
        this.context = context;
        this.options = options;
        this.rest = rest;
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
            lock (sync)
            {
                ready = started;
                counting = false;
                catchingUp = false;
                buffered.Clear();
                lastSeq = 0;
            }

            var hub = Build(uri);
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

    private HubConnection Build(Uri uri)
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
        hub.On<EventListResponseItem>(HubMethods.Event, OnEvent);
        hub.On<long>(HubMethods.Ready, OnReady);
        hub.Closed += _ => RestartAsync(hub);
        return hub;
    }

    private ValueTask StopAsync()
    {
        var current = connection;
        connection = null;
        return current?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    // つなぎ直しをあきらめて閉じたとき (サーバが切った) は、間をおいて始め直す (ready を受けたら抜けた通知を読む)
    private async Task RestartAsync(HubConnection hub)
    {
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

    // はじめは数え始めの位置にし、つなぎ直したときは抜けた通知を読む
    private void OnReady(long seq)
    {
        TaskCompletionSource<long>? first = null;
        lock (sync)
        {
            if (!counting)
            {
                counting = true;
                lastSeq = seq;
                first = ready;
                Flush();
            }
            else
            {
                catchingUp = true;
            }
        }

        if (first is not null)
        {
            first.TrySetResult(seq);
            return;
        }

        _ = CatchUpAsync(seq);
    }

    private void OnEvent(EventListResponseItem item)
    {
        lock (sync)
        {
            if (!counting || catchingUp)
            {
                buffered.Add(item);
                return;
            }

            Enqueue(item);
        }
    }

    // 抜けた通知を読み、ためた通知と合わせて seq の順に渡す。追いかけられなければ今の番号から数え直し、読み直すように知らせる
    private async Task CatchUpAsync(long readySeq)
    {
        long after;
        lock (sync)
        {
            after = lastSeq;
        }

        var result = await rest.GetAsync(String.Create(CultureInfo.InvariantCulture, $"events?after={after}"), ClientJsonContext.Default.EventListResponse, CancellationToken.None);
        lock (sync)
        {
            if (result.Content is { } content)
            {
                buffered.AddRange(content.Items);
                Flush();
                lastSeq = Math.Max(lastSeq, content.LastSeq);
            }
            else
            {
                buffered.Clear();
                lastSeq = Math.Max(lastSeq, readySeq);
                dispatching.Writer.TryWrite(null);
            }

            catchingUp = false;
        }
    }

    // ためた通知を seq の順に渡す (ロックの中で呼ぶ)
    private void Flush()
    {
        foreach (var item in buffered.OrderBy(static x => x.Seq))
        {
            Enqueue(item);
        }

        buffered.Clear();
    }

    // 見た通し番号より後の通知だけを渡す (ロックの中で呼ぶ)。扱わない種類も番号は進める
    private void Enqueue(EventListResponseItem item)
    {
        if (item.Seq <= lastSeq)
        {
            return;
        }

        lastSeq = item.Seq;
        if (Map(item) is { } e)
        {
            dispatching.Writer.TryWrite(e);
        }
    }

    private async Task DispatchAsync()
    {
        await foreach (var e in dispatching.Reader.ReadAllAsync())
        {
            if (e is null)
            {
                Expired?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Received?.Invoke(this, new OrderEventArgs(e));
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Map
    //--------------------------------------------------------------------------------

    // テーブル端末の扱う通知の形にする (扱わない種類と、読めない中身は渡さない)
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
