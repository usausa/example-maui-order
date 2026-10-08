namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Accessors;

public sealed class StoreService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly EventService eventService;

    public StoreService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // 要求した端末の店舗 (トークンの店舗)
    public async ValueTask<StoreResponse?> GetStoreAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var store = await storeAccessor.QueryAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        return store is null ? null : ToResponse(store, context.Now);
    }

    // 注文の一時停止と再開。再開では文言を消す
    public async ValueTask<ServiceError?> SetOrderingAsync(StoreOrderingRequest request, CancellationToken cancellationToken)
    {
        var message = request.Paused ? request.Message : null;
        if ((message is not null) &&
            (String.IsNullOrWhiteSpace(message.Ja) || (message.Ja.Length > Length.PausedMessage) || (message.En?.Length > Length.PausedMessage)))
        {
            return ServiceError.Validation("message", $"文言は日本語を必ず入れ、言語ごとに {Length.PausedMessage} 文字までで送ってください");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            if (await storeAccessor.UpdateOrderingAsync(transaction.Tx, tenantId, storeId, request.Paused, message, context.Now, cancellationToken) == 0)
            {
                return ServiceError.NotFound;
            }

            var store = await storeAccessor.QueryAsync(transaction.Tx, tenantId, storeId, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.StoreUpdated, ToResponse(store!, context.Now), null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    // 営業日は今の時刻と開店の時刻から求める (列に持たない)。店舗の通知 (設定の変更) にも使う
    internal static StoreResponse ToResponse(StoreEntity store, DateTimeOffset now) =>
        new()
        {
            Id = store.Id,
            Code = store.Code,
            Name = store.Name,
            TimeZone = store.TimeZone,
            BusinessDate = StoreHours.BusinessDate(now, store.TimeZone, StoreHours.Parse(store.OpenTime)),
            OpenTime = store.OpenTime,
            CloseTime = store.CloseTime,
            LastOrderTime = store.LastOrderTime,
            OrderingPaused = store.OrderingPaused,
            PausedMessage = store.PausedMessage,
            TaxRounding = store.TaxRounding,
            SettingsVersion = store.SettingsVersion
        };

    //--------------------------------------------------------------------------------
    // Table
    //--------------------------------------------------------------------------------

    // テーブルと今の来店の要約 (表示順)。状態を送ると、その状態のテーブルだけにする
    public async ValueTask<ServiceResult<TableListResponse>> GetTablesAsync(string? status, CancellationToken cancellationToken)
    {
        TableStatus? filter = null;
        if (!String.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<TableStatus>(status, true, out var value) || !Enum.IsDefined(value))
            {
                return new(ServiceError.Validation("status", "Vacant / Occupied / Paying のどれかを送ってください"));
            }

            filter = value;
        }

        var context = contextProvider.Current;
        var tables = await storeAccessor.QueryTableSummaryListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        return new(new TableListResponse
        {
            Items = tables
                .Where(x => (filter is null) || (StatusOf(x) == filter))
                .Select(static x => new TableListResponseItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Area = x.Area,
                    Capacity = x.Capacity,
                    SortOrder = x.SortOrder,
                    Visit = x.VisitId is { } visitId
                        ? new TableListResponseVisit
                        {
                            VisitId = visitId,
                            Adults = x.Adults ?? 0,
                            Children = x.Children ?? 0,
                            Status = x.VisitStatus ?? VisitStatus.Open,
                            OpenedAt = x.OpenedAt ?? default,
                            LastOrderedAt = x.LastOrderedAt,
                            UnservedCount = x.UnservedCount,
                            OpenCallCount = x.OpenCallCount,
                            Version = x.VisitVersion ?? 0
                        }
                        : null
                })
                .ToList()
        });
    }

    private static TableStatus StatusOf(TableSummaryEntity table) =>
        table.VisitStatus switch
        {
            null => TableStatus.Vacant,
            VisitStatus.Paying => TableStatus.Paying,
            _ => TableStatus.Occupied
        };
}
