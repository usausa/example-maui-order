namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;

// 通知の送る先。Kinds の端末には店舗のその種類の通知をすべて送り、Tables / Stations は通知が持つテーブル・持ち場の端末だけに送る
public sealed record EventRoute(IReadOnlyList<DeviceKind> Kinds, bool Tables = false, bool Stations = false);

// 通知の種類ごとの送る先 (ハブで送るときと、抜けた通知を読み出すときで同じものを使う)
public static class EventRoutes
{
    private static readonly EventRoute None = new([]);

    private static readonly Dictionary<string, EventRoute> Routes = new(StringComparer.Ordinal)
    {
        [EventTypes.VisitOpened] = new([DeviceKind.Hall, DeviceKind.Reception], Tables: true),
        [EventTypes.VisitUpdated] = new([DeviceKind.Hall], Tables: true),
        [EventTypes.VisitMoved] = new([DeviceKind.Hall, DeviceKind.Reception], Tables: true),
        [EventTypes.VisitClosed] = new([DeviceKind.Hall, DeviceKind.Reception], Tables: true),
        [EventTypes.OrderCreated] = new([DeviceKind.Hall], Tables: true),
        [EventTypes.OrderLinesUpdated] = new([DeviceKind.Hall], Tables: true),
        [EventTypes.TicketCreated] = new([], Stations: true),
        [EventTypes.TicketUpdated] = new([], Stations: true),
        [EventTypes.CallCreated] = new([DeviceKind.Hall], Tables: true),
        [EventTypes.CallUpdated] = new([DeviceKind.Hall], Tables: true),
        [EventTypes.StockUpdated] = new([DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen]),
        [EventTypes.MenuPublished] = new([DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen]),
        [EventTypes.StoreUpdated] = new([DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen, DeviceKind.Reception]),
        [EventTypes.DeviceUpdated] = new([DeviceKind.Table, DeviceKind.Hall, DeviceKind.Kitchen, DeviceKind.Reception]),
        [EventTypes.PaymentUpdated] = new([DeviceKind.Hall], Tables: true)
    };

    // 知らない種類はどこにも送らない
    public static EventRoute Find(string type) => Routes.GetValueOrDefault(type) ?? None;

    // 端末が受ける通知か (テーブルが null の通知は店舗のすべてのテーブル、持ち場が null の通知はすべての持ち場に送る)
    public static bool IsFor(EventRoute route, IEnumerable<Guid>? tableIds, Guid? stationId, DeviceKind kind, Guid? tableId, IEnumerable<Guid> stationIds)
    {
        if (route.Kinds.Contains(kind))
        {
            return true;
        }

        if ((kind == DeviceKind.Table) && route.Tables)
        {
            return (tableIds is null) || ((tableId is { } id) && tableIds.Contains(id));
        }

        if ((kind == DeviceKind.Kitchen) && route.Stations)
        {
            return (stationId is not { } station) || stationIds.Contains(station);
        }

        return false;
    }
}
