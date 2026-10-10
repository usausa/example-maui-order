namespace TableOrder.Server.Core.Accessors;

// チケット (注文の明細を持ち場ごとに分けたもの) と、その明細
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class KitchenAccessor
{
    [Execute]
    public partial ValueTask<int> InsertTicketAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid stationId, Guid orderId, Guid visitId, DateTimeOffset now, CancellationToken cancellationToken);

    // 店舗のチケット (テーブルの名前と注文の通し番号を足す)
    [QueryFirst]
    public partial ValueTask<KitchenTicketEntity?> QueryTicketAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 来店の開いているチケット (古い順)
    [Query]
    public partial ValueTask<List<KitchenTicketEntity>> QueryOpenTicketListByVisitAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 開いているチケット (古い順)
    [Query]
    public partial ValueTask<List<KitchenTicketEntity>> QueryOpenTicketListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 持ち場の下げたチケット (下げた新しい順に limit 件) と、その明細とオプション (チケットの一覧を持ち場ごとに 1 回で作る)
    [Query]
    public partial ValueTask<List<KitchenTicketEntity>> QueryDoneTicketListByStationAsync(Guid tenantId, Guid storeId, Guid stationId, int limit, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryDoneTicketLineListByStationAsync(Guid tenantId, Guid storeId, Guid stationId, int limit, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryDoneTicketLineOptionListByStationAsync(Guid tenantId, Guid storeId, Guid stationId, int limit, CancellationToken cancellationToken);

    // 下げる (Open から)
    [Execute]
    public partial ValueTask<int> UpdateTicketDoneAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 下げたチケットを戻す (Done から)
    [Execute]
    public partial ValueTask<int> UpdateTicketReopenedAsync(DbTransaction tx, Guid tenantId, Guid id, CancellationToken cancellationToken);

    // チケットの明細 (明細の番号の順) とオプション
    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryTicketLineListAsync(DbTransaction tx, Guid tenantId, Guid ticketId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryTicketLineOptionListAsync(DbTransaction tx, Guid tenantId, Guid ticketId, CancellationToken cancellationToken);

    // 開いているチケットの明細とオプション (チケットの一覧を 1 回で作る)
    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryOpenTicketLineListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryOpenTicketLineOptionListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);
}
