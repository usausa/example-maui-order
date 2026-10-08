namespace TableOrder.Server.Core.Accessors;

// 注文と明細、明細のオプション
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class OrderAccessor
{
    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 店舗の注文 (同じ Id の送り直しを見分ける)
    [QueryFirst]
    public partial ValueTask<OrderEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 要求の内容のハッシュが同じ注文の数 (同じ Id の送り直しの内容が同じかを比べる。ハッシュは行の型に持たない)
    [ExecuteScalar]
    public partial ValueTask<long> CountByRequestHashAsync(DbTransaction tx, Guid tenantId, Guid id, byte[] requestHash, CancellationToken cancellationToken);

    // 来店の注文 (通し番号の順)
    [Query]
    public partial ValueTask<List<OrderEntity>> QueryListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderEntity>> QueryListAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 来店の次の通し番号
    [ExecuteScalar]
    public partial ValueTask<long> QueryNextOrderNoAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid visitId, int orderNo, OrderSource source, Guid? deviceId, string? staffId, string menuVersion, byte[] requestHash, DateTimeOffset now, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    // 来店の明細 (注文の通し番号と明細の番号の順)
    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryLineListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryLineListAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 店舗の明細
    [QueryFirst]
    public partial ValueTask<OrderLineEntity?> QueryLineAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 注文の次の明細の番号 (取消で明細を分けるとき)
    [ExecuteScalar]
    public partial ValueTask<long> QueryNextLineNoAsync(DbTransaction tx, Guid tenantId, Guid orderId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid orderId, Guid visitId, int lineNo, Guid itemId, string itemCode, LocalizedText name, string tags, int quantity, decimal unitPrice, decimal amount, decimal taxRate, OrderTiming timing, OrderLineStatus status, Guid? stationId, ServedBy servedBy, Guid? ticketId, DateTimeOffset? releasedAt, DateTimeOffset? readyAt, DateTimeOffset? servedAt, CancellationToken cancellationToken);

    // 数量の一部を取り消すときに、取り消す分を元の明細から写して取消の明細にする
    [Execute]
    public partial ValueTask<int> InsertLineSplitAsync(DbTransaction tx, Guid tenantId, Guid id, Guid fromId, int lineNo, int quantity, string? reason, string? staffId, DateTimeOffset now, CancellationToken cancellationToken);

    // 数量を足し引きする (一部の取消で明細を分けるとき。1 つは残す)
    [Execute]
    public partial ValueTask<int> AddLineQuantityAsync(DbTransaction tx, Guid tenantId, Guid id, int quantity, CancellationToken cancellationToken);

    // 食後の品をお願いする (Held から、作る品は Ordered、作らない品は Ready、お客様がとる品は Served)
    [Execute]
    public partial ValueTask<int> UpdateLineReleasedAsync(DbTransaction tx, Guid tenantId, Guid id, OrderLineStatus status, Guid? ticketId, DateTimeOffset? readyAt, DateTimeOffset? servedAt, DateTimeOffset now, CancellationToken cancellationToken);

    // 作り始め (Ordered から)
    [Execute]
    public partial ValueTask<int> UpdateLineStartedAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // できあがり (Ordered か Cooking から)
    [Execute]
    public partial ValueTask<int> UpdateLineReadyAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 提供 (Ordered、Cooking、Ready から)
    [Execute]
    public partial ValueTask<int> UpdateLineServedAsync(DbTransaction tx, Guid tenantId, Guid id, string? staffId, DateTimeOffset now, CancellationToken cancellationToken);

    // 下げたチケットを戻す (Ready から Cooking)
    [Execute]
    public partial ValueTask<int> UpdateLineRecalledAsync(DbTransaction tx, Guid tenantId, Guid id, CancellationToken cancellationToken);

    // 取消 (提供の前から)
    [Execute]
    public partial ValueTask<int> UpdateLineCancelledAsync(DbTransaction tx, Guid tenantId, Guid id, string? reason, string? staffId, DateTimeOffset now, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Option
    //--------------------------------------------------------------------------------

    [Execute]
    public partial ValueTask<int> InsertLineOptionAsync(DbTransaction tx, Guid tenantId, Guid lineId, int sortOrder, Guid optionGroupId, Guid optionId, LocalizedText name, decimal priceDelta, CancellationToken cancellationToken);

    // 分けた明細に、元の明細のオプションを写す
    [Execute]
    public partial ValueTask<int> InsertLineOptionSplitAsync(DbTransaction tx, Guid tenantId, Guid lineId, Guid fromLineId, CancellationToken cancellationToken);

    // 来店の明細のオプション
    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryLineOptionListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryLineOptionListAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Serving
    //--------------------------------------------------------------------------------

    // 店舗の開いている来店の、スタッフが運ぶ品の提供の前の明細 (開発の環境で時間で進める)
    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryProgressLineListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 店舗の開いている来店の、その状態の明細とテーブル (できあがりの古い順)
    [Query]
    public partial ValueTask<List<ServingLineEntity>> QueryServingLineListAsync(Guid tenantId, Guid storeId, OrderLineStatus status, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderLineOptionEntity>> QueryServingLineOptionListAsync(Guid tenantId, Guid storeId, OrderLineStatus status, CancellationToken cancellationToken);
}
