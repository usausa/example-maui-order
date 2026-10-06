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

// 来店を閉じた (visit.closed。テーブルやレジで払い終えた、スタッフが閉じた)
public sealed record VisitClosedEvent(long Seq, DateTimeOffset OccurredAt, VisitResponse Visit) : OrderEvent(Seq, OccurredAt);

// 店舗が変わった (store.updated。注文の一時停止、ラストオーダー)
public sealed record StoreUpdatedEvent(long Seq, DateTimeOffset OccurredAt, StoreResponse Store) : OrderEvent(Seq, OccurredAt);
