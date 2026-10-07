namespace TableOrder.Terminal.Table.State;

// 起動のときに読んだ店舗の設定・メニュー・品切れ。画面には選んでいる言語に直した形で渡す
public sealed class MenuState
{
    private Dictionary<Guid, MenuResponseItem> items = [];

    private Dictionary<Guid, MenuResponseOptionGroup> groups = [];

    private Dictionary<Guid, MenuResponseOption> options = [];

    private Dictionary<string, MenuResponseAllergen> allergens = [];

    private Dictionary<Guid, StockResponseItem> stocks = [];

    public DeviceConfigResponse Config { get; private set; } = default!;

    public MenuResponse Menu { get; private set; } = default!;

    // この端末を置いたテーブル (管理画面で割り当て、端末の設定で受け取る)
    public string? TableName => Config.Device?.TableName;

    public void Update(DeviceConfigResponse config, MenuResponse menu, StockResponse stock)
    {
        Config = config;
        Menu = menu;

        items = menu.Items.ToDictionary(static x => x.Id);
        groups = menu.OptionGroups.ToDictionary(static x => x.Id);
        options = menu.OptionGroups.SelectMany(static x => x.Options).ToDictionary(static x => x.Id);
        allergens = menu.Allergens.ToDictionary(static x => x.Code, StringComparer.Ordinal);

        UpdateStock(stock);
    }

    public void UpdateStock(StockResponse stock)
    {
        stocks = stock.Items.ToDictionary(static x => x.TargetId);
    }

    //--------------------------------------------------------------------------------
    // Display
    //--------------------------------------------------------------------------------

    // カテゴリ (表示順) と、その中の商品
    public IReadOnlyList<MenuCategory> GetCategories(Language language) =>
        Menu.Categories
            .OrderBy(static x => x.SortOrder)
            .Select(x => new MenuCategory(
                x.Id,
                x.Name.Get(language),
                x.ItemIds
                    .Where(items.ContainsKey)
                    .Select(id => ToProduct(items[id], language))
                    .ToList()))
            .ToList();

    private MenuProduct ToProduct(MenuResponseItem item, Language language) =>
        new(item.Id, item.Name.Get(language), item.Price, item.ImageName, item.Badges, IsSoldOut(item.Id));

    public string AllergenName(string code, Language language) =>
        allergens.TryGetValue(code, out var allergen) ? allergen.Name.Get(language) : code;

    public string TagName(string code, Language language) =>
        Menu.Tags.FirstOrDefault(x => x.Code == code)?.Name.Get(language) ?? code;

    // 選んだオプションの名前 (例: デミグラス / ライス・スープセット)
    public string OptionText(IEnumerable<Guid> optionIds, Language language) =>
        String.Join(" / ", optionIds.Where(options.ContainsKey).Select(x => options[x].Name.Get(language)));

    //--------------------------------------------------------------------------------
    // Lookup
    //--------------------------------------------------------------------------------

    public MenuResponseItem GetItem(Guid id) => items[id];

    public MenuResponseOption GetOption(Guid id) => options[id];

    public IEnumerable<MenuResponseOptionGroup> GetOptionGroups(MenuResponseItem item) =>
        item.OptionGroupIds.Where(groups.ContainsKey).Select(x => groups[x]);

    public bool IsSoldOut(Guid id) =>
        stocks.TryGetValue(id, out var stock) && (stock.Status == StockStatus.SoldOut);

    // 残りの数 (Limited のときだけ)
    public int? Remaining(Guid id) =>
        stocks.TryGetValue(id, out var stock) && (stock.Status == StockStatus.Limited) ? stock.Remaining : null;

    // 1 明細の数量の上限 (商品の上限が店舗の上限より厳しければ商品の上限)
    public int MaxQuantity(MenuResponseItem item) =>
        Math.Min(item.MaxQuantity ?? Int32.MaxValue, Config.OrderRules.MaxQuantityPerLine);

    public decimal UnitPrice(Guid itemId, IEnumerable<Guid> optionIds) =>
        Pricing.UnitPrice(items[itemId].Price, optionIds.Where(options.ContainsKey).Select(x => options[x].PriceDelta));

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
