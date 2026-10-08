namespace TableOrder.Client;

// 注文サーバからの通知。seq は店舗の中の通し番号
public abstract record OrderEvent(long Seq, DateTimeOffset OccurredAt);

public sealed class OrderEventArgs : EventArgs
{
    public OrderEvent Event { get; }

    public OrderEventArgs(OrderEvent e)
    {
        Event = e;
    }
}

// ホール端末などで来店を開いた (visit.opened)
public sealed record VisitOpenedEvent(long Seq, DateTimeOffset OccurredAt, VisitResponse Visit) : OrderEvent(Seq, OccurredAt);

// 来店が変わった (visit.updated。人数の変更、ホール端末で会計を始めた・やめた)
public sealed record VisitUpdatedEvent(long Seq, DateTimeOffset OccurredAt, VisitResponse Visit) : OrderEvent(Seq, OccurredAt);

// 来店をほかのテーブルに移した (visit.moved。元のテーブルは待受に、移った先のテーブルは注文の画面にする)
public sealed record VisitMovedEvent(long Seq, DateTimeOffset OccurredAt, VisitResponse Visit) : OrderEvent(Seq, OccurredAt);

// 来店を閉じた (visit.closed。テーブルやレジで払い終えた、スタッフが閉じた)
public sealed record VisitClosedEvent(long Seq, DateTimeOffset OccurredAt, VisitResponse Visit) : OrderEvent(Seq, OccurredAt);

// 店舗が変わった (store.updated。注文の一時停止、ラストオーダー)
public sealed record StoreUpdatedEvent(long Seq, DateTimeOffset OccurredAt, StoreResponse Store) : OrderEvent(Seq, OccurredAt);

// 品切れが変わった (stock.updated。変わった品だけで、売れるように戻した品は Available)
public sealed record StockUpdatedEvent(long Seq, DateTimeOffset OccurredAt, IReadOnlyList<StockResponseItem> Items) : OrderEvent(Seq, OccurredAt);

// 注文を受けた (order.created。この端末で送った注文と、ホール端末で代わりに受けた注文)
public sealed record OrderCreatedEvent(long Seq, DateTimeOffset OccurredAt, OrderListResponseItem Order) : OrderEvent(Seq, OccurredAt);

// 明細の状態が変わった (order.lines.updated。調理、提供、取消、食後の品のお願い)。変わった注文を、注文のすべての明細と一緒に受ける
public sealed record OrderLinesUpdatedEvent(long Seq, DateTimeOffset OccurredAt, Guid VisitId, IReadOnlyList<OrderListResponseItem> Orders) : OrderEvent(Seq, OccurredAt);

// 管理画面で端末の置き場所・名前を替えたか、無効にした (device.updated。店舗のすべての端末に届き、その端末だけが起動からやり直す)
public sealed record DeviceUpdatedEvent(long Seq, DateTimeOffset OccurredAt, Guid DeviceId) : OrderEvent(Seq, OccurredAt);
