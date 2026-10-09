namespace TableOrder.KitchenApp.State;

// 品切れと残りの数 (品とオプションの id ごと)。起動で読み、通知 (stock.updated) の変わった品で替える
public sealed class StockState
{
    private Dictionary<Guid, StockResponseItem> stocks = [];

    public StockResponseItem? Find(Guid targetId) =>
        stocks.GetValueOrDefault(targetId);

    public void Update(StockResponse stock) =>
        stocks = stock.Items.ToDictionary(static x => x.TargetId);

    // 変わった品だけを替える (売れるように戻した品は Available で届く)
    public void Apply(IEnumerable<StockResponseItem> changes)
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
