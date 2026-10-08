namespace TableOrder.Client.Rest;

using Microsoft.AspNetCore.Http.Connections;

// 注文サーバへのつなぎ方。端末は既定のまま使い、テストはサーバの中のハンドラと Long Polling でつなぐ
public sealed class OrderServerOptions
{
    // 要求と通知のハブの通信のハンドラを作る (null は既定のハンドラ)
    public Func<HttpMessageHandler>? HandlerFactory { get; init; }

    // 通知のハブの方式
    public HttpTransportType HubTransports { get; init; } = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
}
