namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Accessors;

public sealed class StoreService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    public StoreService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
    }

    // 要求した端末の店舗 (トークンの店舗)
    public async ValueTask<StoreResponse?> GetStoreAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var store = await storeAccessor.QueryAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        return store is null ? null : ToResponse(store, context.Now);
    }

    // 営業日は今の時刻と開店の時刻から求める (列に持たない)
    private static StoreResponse ToResponse(StoreEntity store, DateTimeOffset now) =>
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
            TaxRounding = store.TaxRounding
        };
}
