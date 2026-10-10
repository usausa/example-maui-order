namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Events;
using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;

public sealed class StockService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly StockAccessor stockAccessor;

    private readonly MenuService menuService;

    private readonly EventService eventService;

    public StockService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        StockAccessor stockAccessor,
        MenuService menuService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.stockAccessor = stockAccessor;
        this.menuService = menuService;
        this.eventService = eventService;
    }

    // Available でない品 (行があるものだけ)
    public async ValueTask<StockResponse> GetStockAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var stocks = await stockAccessor.QueryListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        return new StockResponse
        {
            Items = stocks.Select(ToItem).ToList()
        };
    }

    // 品切れと残りの数の設定。残りの数を 0 にしたら SoldOut、Available に戻したら行を消す
    // 品は今のメニューの商品かオプション (ないものは NOT_FOUND)。変わらなければ (同じ設定の送り直し) 通知を書かない
    public async ValueTask<ServiceError?> UpdateAsync(Guid targetId, StockUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TargetKind))
        {
            return ServiceError.Validation("targetKind", "Item か Option を送ってください");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return ServiceError.Validation("status", "Available / Limited / SoldOut のどれかを送ってください");
        }

        if ((request.Status == StockStatus.Limited) && (request.Remaining is not (>= 0 and <= Length.MaxStockRemaining)))
        {
            return ServiceError.Validation("remaining", $"残りの数を 0 から {Length.MaxStockRemaining} で送ってください");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        var catalog = store is null ? null : await menuService.GetCatalogAsync(store, cancellationToken);
        var exists = request.TargetKind == StockTargetKind.Item
            ? catalog?.Items.ContainsKey(targetId) ?? false
            : catalog?.Options.ContainsKey(targetId) ?? false;
        if (!exists)
        {
            return ServiceError.NotFound;
        }

        var status = (request.Status == StockStatus.Limited) && (request.Remaining == 0) ? StockStatus.SoldOut : request.Status;
        var remaining = status == StockStatus.Limited ? request.Remaining : null;
        return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            if (status == StockStatus.Available)
            {
                if (await stockAccessor.DeleteAsync(transaction.Tx, tenantId, storeId, targetId, cancellationToken) == 0)
                {
                    return null;
                }
            }
            else
            {
                var current = (await stockAccessor.QueryListAsync(transaction.Tx, tenantId, storeId, cancellationToken)).Find(x => x.TargetId == targetId);
                if ((current is not null) && (current.TargetKind == request.TargetKind) && (current.Status == status) && (current.Remaining == remaining))
                {
                    return null;
                }

                await stockAccessor.UpsertAsync(transaction.Tx, tenantId, storeId, targetId, request.TargetKind, status, remaining, context.Now, cancellationToken);
            }

            var data = new StockUpdatedEventData
            {
                Items =
                [
                    new StockResponseItem
                    {
                        TargetId = targetId,
                        TargetKind = request.TargetKind,
                        Status = status,
                        Remaining = remaining,
                        UpdatedAt = context.Now
                    }
                ]
            };
            await transaction.AppendEventAsync(EventTypes.StockUpdated, data, null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    // すべて Available に戻す (営業日の始めなど)。戻した品を通知する
    public async ValueTask ResetAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        await eventService.WriteAsync(tenantId, storeId, async transaction =>
        {
            var stocks = await stockAccessor.QueryListAsync(transaction.Tx, tenantId, storeId, cancellationToken);
            if (stocks.Count == 0)
            {
                return false;
            }

            await stockAccessor.DeleteAllAsync(transaction.Tx, tenantId, storeId, cancellationToken);
            var data = new StockUpdatedEventData
            {
                Items = stocks.Select(x => new StockResponseItem
                {
                    TargetId = x.TargetId,
                    TargetKind = x.TargetKind,
                    Status = StockStatus.Available,
                    UpdatedAt = context.Now
                }).ToList()
            };
            await transaction.AppendEventAsync(EventTypes.StockUpdated, data, null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    internal static StockResponseItem ToItem(StockEntity stock) =>
        new()
        {
            TargetId = stock.TargetId,
            TargetKind = stock.TargetKind,
            Status = stock.Status,
            Remaining = stock.Remaining,
            UpdatedAt = stock.UpdatedAt
        };
}
