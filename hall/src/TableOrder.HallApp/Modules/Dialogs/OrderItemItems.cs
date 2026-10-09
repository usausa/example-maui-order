namespace TableOrder.HallApp.Modules.Dialogs;

// 入れる商品と、1 明細の数量の上限
public sealed record OrderItemParameter(Guid ItemId, int MaxQuantity);

// オプションの組 (ソース、セットなど)。必須の組は選ぶまで入れられない
public sealed class OrderOptionGroup
{
    public string Name { get; }

    public string RuleText { get; }

    public bool IsRequired { get; }

    public int MinSelect { get; }

    public int MaxSelect { get; }

    public IReadOnlyList<OrderOptionChoice> Options { get; }

    public bool IsSatisfied => Options.Count(static x => x.IsSelected) >= MinSelect;

    // 既定のオプションを選んでおく (品切れのものは選ばない)
    public OrderOptionGroup(MenuResponseOptionGroup group, MenuState menuState)
    {
        Name = ViewHelper.Text(group.Name);
        IsRequired = group.MinSelect > 0;
        MinSelect = group.MinSelect;
        MaxSelect = group.MaxSelect;
        RuleText = group.MaxSelect > 1
            ? ViewHelper.Format(AppResources.ItemChooseUpToFormat, group.MaxSelect)
            : IsRequired ? AppResources.ItemChooseOne : AppResources.ItemOptional;
        Options = group.Options
            .Select(x => new OrderOptionChoice(this, x, menuState.IsSoldOut(x.Id)))
            .ToList();
    }
}

// オプションの選択肢。品切れのオプションは選べない
public sealed partial class OrderOptionChoice : ObservableObject
{
    public OrderOptionGroup Group { get; }

    public Guid Id { get; }

    public string Name { get; }

    public decimal PriceDelta { get; }

    // 価格の差 (例: +¥100)。品切れは品切れと出す
    public string CaptionText { get; }

    public bool IsSoldOut { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public OrderOptionChoice(OrderOptionGroup group, MenuResponseOption option, bool soldOut)
    {
        Group = group;
        Id = option.Id;
        Name = ViewHelper.Text(option.Name);
        PriceDelta = option.PriceDelta;
        IsSoldOut = soldOut;
        CaptionText = soldOut ? AppResources.StockSoldOut : ViewHelper.PriceDelta(option.PriceDelta);
        IsSelected = option.IsDefault && !soldOut;
    }
}

// 出す時機 (すぐに / 食後に)
public sealed partial class OrderTimingChoice : ObservableObject
{
    public OrderTiming Timing { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public OrderTimingChoice(OrderTiming timing, string name, bool selected)
    {
        Timing = timing;
        Name = name;
        IsSelected = selected;
    }
}
