namespace TableOrder.HallApp.State;

// メニューと品切れ (品切れと代わりの注文の画面で使う)。起動のときに読み、品切れは通知 (stock.updated) で替える
public sealed class MenuState
{
    private Dictionary<Guid, StockResponseItem> stocks = [];

    public MenuResponse Menu { get; private set; } = default!;

    // 売れない品と残りの数のある品 (売れる品は持たない)
    public IReadOnlyCollection<StockResponseItem> Stocks => stocks.Values;

    public void Update(MenuResponse menu, StockResponse stock)
    {
        Menu = menu;
        stocks = stock.Items.ToDictionary(static x => x.TargetId);
    }

    // 通知で受けた変わった品だけを入れる (売れるように戻した品は除く)
    public void ApplyStock(IEnumerable<StockResponseItem> changes)
    {
        foreach (var change in changes)
        {
            if (change.Status == StockStatus.Available)
            {
                stocks.Remove(change.TargetId);
            }
            else
            {
                stocks[change.TargetId] = change;
            }
        }
    }
}
