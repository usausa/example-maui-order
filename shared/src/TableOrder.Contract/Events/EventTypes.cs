namespace TableOrder.Contract.Events;

// 通知の種類 (type)。送る先の端末は種類で決まる
public static class EventTypes
{
    public const string VisitOpened = "visit.opened";

    public const string VisitUpdated = "visit.updated";

    public const string VisitMoved = "visit.moved";

    public const string VisitClosed = "visit.closed";

    public const string OrderCreated = "order.created";

    public const string OrderLinesUpdated = "order.lines.updated";

    public const string TicketCreated = "ticket.created";

    public const string TicketUpdated = "ticket.updated";

    public const string CallCreated = "call.created";

    public const string CallUpdated = "call.updated";

    public const string StockUpdated = "stock.updated";

    public const string MenuPublished = "menu.published";

    public const string StoreUpdated = "store.updated";

    public const string DeviceUpdated = "device.updated";

    public const string PaymentUpdated = "payment.updated";
}
