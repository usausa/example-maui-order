namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Calls;
using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;

// 店内の今の呼び出し (用件の名前を添える)
public sealed record FloorCallResult(CallListResponseItem Call, LocalizedText ReasonName);

// 店内の今の品切れ (今のメニューの名前を添える。メニューにない品は名前なし)
public sealed record FloorStockResult(StockResponseItem Stock, LocalizedText? Name);

// 店内の今 (管理画面) で、端末と同じ応答に名前を添えて読む。来店・注文・案内は、端末と同じ Service をそのまま呼ぶ
public sealed class FloorService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly StoreAccessor storeAccessor;

    private readonly SettingsAccessor settingsAccessor;

    private readonly MenuService menuService;

    private readonly StockService stockService;

    private readonly CallService callService;

    public FloorService(
        ServiceContextProvider contextProvider,
        StoreAccessor storeAccessor,
        SettingsAccessor settingsAccessor,
        MenuService menuService,
        StockService stockService,
        CallService callService)
    {
        this.contextProvider = contextProvider;
        this.storeAccessor = storeAccessor;
        this.settingsAccessor = settingsAccessor;
        this.menuService = menuService;
        this.stockService = stockService;
        this.callService = callService;
    }

    // 終わっていない呼び出し (古い順)。呼んだあとに使わなくした用件も名前で出す
    public async ValueTask<List<FloorCallResult>> GetCallsAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var calls = (await callService.GetStoreListAsync(null, cancellationToken)).Value?.Items ?? [];
        var reasons = (await settingsAccessor.QueryCallReasonAllAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken))
            .ToDictionary(static x => x.Code, static x => x.Name);
        return calls.Select(x => new FloorCallResult(x, reasons.GetValueOrDefault(x.ReasonCode) ?? new LocalizedText { Ja = x.ReasonCode })).ToList();
    }

    // 売れない品と残りの数
    public async ValueTask<List<FloorStockResult>> GetStockAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var stock = await stockService.GetStockAsync(cancellationToken);
        var store = await storeAccessor.QueryAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        var catalog = store is null ? null : await menuService.GetCatalogAsync(store, cancellationToken);
        return stock.Items.Select(x => new FloorStockResult(x, NameOf(catalog, x))).ToList();
    }

    private static LocalizedText? NameOf(MenuCatalog? catalog, StockResponseItem stock)
    {
        if (catalog is null)
        {
            return null;
        }

        if (stock.TargetKind == StockTargetKind.Item)
        {
            return catalog.Items.GetValueOrDefault(stock.TargetId)?.Name;
        }

        return catalog.Options.TryGetValue(stock.TargetId, out var option) ? option.Option.Name : null;
    }
}
