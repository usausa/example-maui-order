namespace TableOrder.HallApp.State;

// メニューと品切れ (品切れと代わりの注文の画面で使う)。起動のときに読み、品切れは通知 (stock.updated) で替える
public sealed class MenuState
{
    private Dictionary<Guid, MenuResponseItem> items = [];

    private Dictionary<Guid, MenuResponseOptionGroup> groups = [];

    private Dictionary<Guid, MenuResponseOption> options = [];

    private Dictionary<Guid, StockResponseItem> stocks = [];

    public MenuResponse Menu { get; private set; } = default!;

    // 売れない品と残りの数のある品 (売れる品は持たない)
    public IReadOnlyCollection<StockResponseItem> Stocks => stocks.Values;

    public void Update(MenuResponse menu, StockResponse stock)
    {
        Menu = menu;
        items = menu.Items.ToDictionary(static x => x.Id);
        groups = menu.OptionGroups.ToDictionary(static x => x.Id);
        options = menu.OptionGroups.SelectMany(static x => x.Options).ToDictionary(static x => x.Id);
        UpdateStock(stock);
    }

    // 読み直した品切れ (品切れのタブで替えたあと)
    public void UpdateStock(StockResponse stock)
    {
        stocks = stock.Items.ToDictionary(static x => x.TargetId);
    }

    // 品の品切れと残りの数 (売れる品は null)
    public StockResponseItem? FindStock(Guid targetId) =>
        stocks.GetValueOrDefault(targetId);

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

    //--------------------------------------------------------------------------------
    // Lookup
    //--------------------------------------------------------------------------------

    public MenuResponseItem? FindItem(Guid id) =>
        items.GetValueOrDefault(id);

    public IEnumerable<MenuResponseOptionGroup> GetOptionGroups(MenuResponseItem item) =>
        item.OptionGroupIds.Where(groups.ContainsKey).Select(x => groups[x]);

    public bool IsSoldOut(Guid id) =>
        stocks.TryGetValue(id, out var stock) && (stock.Status == StockStatus.SoldOut);

    public decimal UnitPrice(Guid itemId, IEnumerable<Guid> optionIds) =>
        Pricing.UnitPrice(items[itemId].Price, optionIds.Where(options.ContainsKey).Select(x => options[x].PriceDelta));

    // 選んだオプションの名前 (画面は端末の言語で選ぶ)
    public IEnumerable<LocalizedText> OptionNames(IEnumerable<Guid> optionIds) =>
        optionIds.Where(options.ContainsKey).Select(x => options[x].Name);

    //--------------------------------------------------------------------------------
    // Rule
    //--------------------------------------------------------------------------------

    public IEnumerable<MenuResponseRule> GetRules(MenuRuleKind kind) =>
        Menu.Rules.Where(x => x.Kind == kind);

    // 商品と選んだオプションのタグ
    public HashSet<string> GetTags(Guid itemId, IEnumerable<Guid> optionIds)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        if (items.TryGetValue(itemId, out var item))
        {
            tags.UnionWith(item.Tags);
        }

        foreach (var id in optionIds)
        {
            if (options.TryGetValue(id, out var option))
            {
                tags.UnionWith(option.Tags);
            }
        }

        return tags;
    }
}
