namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;

public sealed class StockService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StockAccessor stockAccessor;

    public StockService(
        ServiceContextProvider contextProvider,
        StockAccessor stockAccessor)
    {
        this.contextProvider = contextProvider;
        this.stockAccessor = stockAccessor;
    }

    // Available でない品 (行があるものだけ)
    public async ValueTask<StockResponse> GetStockAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var stocks = await stockAccessor.QueryListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        return new StockResponse
        {
            Items = stocks.Select(static x => new StockResponseItem
            {
                TargetId = x.TargetId,
                TargetKind = x.TargetKind,
                Status = x.Status,
                Remaining = x.Remaining,
                UpdatedAt = x.UpdatedAt
            }).ToList()
        };
    }
}
