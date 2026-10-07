namespace TableOrder.Client;

// 注文サーバからの通知。実装は SignalR / gRPC のストリーム / モックを DI で替える
public interface IOrderEvents
{
    // 届いた通知。seq の重複は受ける側で捨てる (どのスレッドで出すかは実装による)
    event EventHandler<OrderEventArgs>? Received;
}
