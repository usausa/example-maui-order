namespace TableOrder.Contract.Events;

using TableOrder.Contract.Menu;

// stock.updated の中身 (変わった品だけ。売れるように戻した品は Available)
public sealed class StockUpdatedEventData
{
    public IReadOnlyList<StockResponseItem> Items { get; set; } = default!;
}
