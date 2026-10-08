namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 送る通知と、送る先
public sealed record EventDelivery(EventListResponseItem Item, EventRoute Route, IReadOnlyList<Guid>? TableIds, Guid? StationId);

// 店舗の書き込みの単位。トランザクションと、コミットしたら送る通知を持つ
public sealed class StoreTransaction
{
    private readonly EventAccessor eventAccessor;

    public DbTransaction Tx { get; }

    public Guid TenantId { get; }

    public Guid StoreId { get; }

    public bool HasEvents { get; private set; }

    public bool Committed { get; private set; }

    internal StoreTransaction(EventAccessor eventAccessor, DbTransaction tx, Guid tenantId, Guid storeId)
    {
        this.eventAccessor = eventAccessor;
        Tx = tx;
        TenantId = tenantId;
        StoreId = storeId;
    }

    // 通知を書く (書き込みと同じトランザクションで書き、コミットしたら送る)。テーブルが null の通知は店舗のすべてのテーブルに送る
    public async ValueTask AppendEventAsync<T>(string type, T data, IReadOnlyList<Guid>? tableIds, Guid? stationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seq = await eventAccessor.AddSeqAsync(Tx, TenantId, StoreId, cancellationToken);
        await eventAccessor.InsertAsync(
            Tx,
            TenantId,
            StoreId,
            seq,
            type,
            now,
            JsonSerializer.Serialize(data, JsonDefaults.Options),
            tableIds is null ? null : JsonSerializer.Serialize(tableIds, JsonDefaults.Options),
            stationId,
            cancellationToken);
        HasEvents = true;
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken)
    {
        await Tx.CommitAsync(cancellationToken);
        Committed = true;
    }
}

public sealed class EventService
{
    // 抜けた通知を一度に返す数 (超えたら端末に今の状態を読み直してもらう)
    private const int CatchUpLimit = 1000;

    // 送っていない通知を一度に読む数
    private const int PendingLimit = 500;

    // 通知を残す時間 (これより前の抜けは、端末に今の状態を読み直してもらう)
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly IEventPublisher publisher;

    private readonly EventAccessor eventAccessor;

    private readonly BackgroundAccessor backgroundAccessor;

    public EventService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        IEventPublisher publisher,
        EventAccessor eventAccessor,
        BackgroundAccessor backgroundAccessor)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.publisher = publisher;
        this.eventAccessor = eventAccessor;
        this.backgroundAccessor = backgroundAccessor;
    }

    //--------------------------------------------------------------------------------
    // Write
    //--------------------------------------------------------------------------------

    // 店舗の書き込み。最初に通し番号の行をとって店舗の書き込みを 1 つずつにし (通知を通し番号の順にコミットする)、コミットしたら通知を送る
    // action がコミットしなければ、書き込みも通知も捨てる
    public async ValueTask<T> WriteAsync<T>(Guid tenantId, Guid storeId, Func<StoreTransaction, ValueTask<T>> action, CancellationToken cancellationToken)
    {
        StoreTransaction? transaction = null;
        var result = await provider.UsingTxAsync(async (_, tx) =>
        {
            await eventAccessor.UpsertSequenceAsync(tx, tenantId, storeId, cancellationToken);
            transaction = new StoreTransaction(eventAccessor, tx, tenantId, storeId);
            return await action(transaction);
        }, cancellationToken);

        if (transaction is { Committed: true, HasEvents: true })
        {
            publisher.Publish(tenantId, storeId);
        }

        return result;
    }

    //--------------------------------------------------------------------------------
    // Catch up
    //--------------------------------------------------------------------------------

    // つなぎ直した端末が抜けた通知 (端末の種類と置き場所に合うものだけ)
    // 通し番号が戻った (DB を作り直した)、残している範囲より前、多すぎるときは EVENTS_EXPIRED にして、今の状態を読み直してもらう
    public async ValueTask<ServiceResult<EventListResponse>> GetEventsAsync(long? after, CancellationToken cancellationToken)
    {
        if (after is not (>= 0 and var last))
        {
            return new(ServiceError.Validation("after", "最後に受けた通し番号 (0 以上) を送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var lastSeq = await eventAccessor.QueryLastSeqAsync(tenantId, storeId, cancellationToken);
        if (last == lastSeq)
        {
            return new(new EventListResponse { LastSeq = lastSeq, Items = [] });
        }

        var firstSeq = await eventAccessor.QueryFirstSeqAsync(tenantId, storeId, cancellationToken);
        if ((last > lastSeq) || (firstSeq == 0) || (firstSeq > last + 1) || (lastSeq - last > CatchUpLimit))
        {
            return new(new ServiceError(ErrorCodes.EventsExpired));
        }

        var events = await eventAccessor.QueryListAsync(tenantId, storeId, last, CatchUpLimit, cancellationToken);
        var kind = context.DeviceKind;
        var items = events
            .Select(ToDelivery)
            .Where(x => (kind is { } k) && EventRoutes.IsFor(x.Route, x.TableIds, x.StationId, k, context.TableId, context.StationIds))
            .Select(static x => x.Item)
            .ToList();
        return new(new EventListResponse
        {
            LastSeq = events.Count > 0 ? Math.Max(lastSeq, events[^1].Seq) : lastSeq,
            Items = items
        });
    }

    // 文脈の店舗の今の通し番号 (ハブにつないだ端末が数え始める位置)
    public ValueTask<long> GetLastSeqAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return eventAccessor.QueryLastSeqAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Background
    //--------------------------------------------------------------------------------

    // すべての店舗の通し番号 (送り始める位置)
    public ValueTask<List<EventSequenceEntity>> GetSequenceAllAsync(CancellationToken cancellationToken) =>
        backgroundAccessor.QueryEventSequenceAllAsync(cancellationToken);

    // 文脈の店舗の、after より後の通知 (通し番号の順)
    public async ValueTask<List<EventDelivery>> GetPendingAsync(long after, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var events = await eventAccessor.QueryListAsync(context.RequireTenantId(), context.RequireStoreId(), after, PendingLimit, cancellationToken);
        return events.Select(ToDelivery).ToList();
    }

    // 残す時間を過ぎた通知を消す
    public ValueTask<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        backgroundAccessor.DeleteEventAsync(now - Retention, cancellationToken);

    private static EventDelivery ToDelivery(EventEntity entity) =>
        new(
            new EventListResponseItem
            {
                Seq = entity.Seq,
                Type = entity.Type,
                OccurredAt = entity.OccurredAt,
                Data = JsonSerializer.Deserialize<JsonElement>(entity.Data, JsonDefaults.Options)
            },
            EventRoutes.Find(entity.Type),
            entity.TableIds is null ? null : JsonSerializer.Deserialize<List<Guid>>(entity.TableIds, JsonDefaults.Options),
            entity.StationId);
}
