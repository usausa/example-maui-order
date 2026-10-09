namespace TableOrder.Client;

// 注文サーバからの通知。実装は通信の方式 (SignalR、gRPC のストリーム) ごとに作る
public interface IOrderEvents
{
    // 届いた通知。seq の重複は受ける側で捨てる (どのスレッドで出すかは実装による)
    // seq は接続 (OrderEventArgs.Connection) の中で数える。接続が替わったら、受ける側も数え直す
    event EventHandler<OrderEventArgs>? Received;

    // 抜けた通知を追いかけられなくなった (端末は今の状態を読み直す)
    event EventHandler? Expired;

    // 通知を受け始める (つないで、受ける準備ができるまで待つ)。起動で今の状態を読む前に呼び、そのあとの通知を取りこぼさない
    // すでにつないでいても、つなぎ直して数え始めの位置を決め直す
    ValueTask<ApiResult<NoContent>> ConnectAsync(CancellationToken cancel = default);
}
