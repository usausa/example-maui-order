namespace TableOrder.Terminal.Table.Modules.Menu;

// カテゴリのタブ (選んでいるタブを主色の面で示す)
public sealed partial class CategoryTab : ObservableObject
{
    public Guid Id { get; }

    public string Name { get; }

    public IReadOnlyList<MenuCard> Cards { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public CategoryTab(Guid id, string name, IReadOnlyList<MenuCard> cards)
    {
        Id = id;
        Name = name;
        Cards = cards;
    }
}

// メニューのカード (料理の絵、名前、価格、印)。オプションのない商品はカードの + ですぐ入れられる
public sealed class MenuCard
{
    public Guid Id { get; }

    public string Name { get; }

    public string PriceText { get; }

    public string? ImageName { get; }

    public string BadgeText { get; }

    public bool HasBadge => BadgeText.Length > 0;

    public bool IsSoldOut { get; }

    public bool CanQuickAdd { get; }

    public MenuCard(MenuProduct product, bool hasOptions)
    {
        Id = product.Id;
        Name = product.Name;
        PriceText = ViewHelper.Price(product.Price);
        ImageName = product.ImageName;
        BadgeText = product.Badges.Count > 0 ? ViewHelper.Name(product.Badges[0]) : string.Empty;
        IsSoldOut = product.IsSoldOut;
        CanQuickAdd = !hasOptions && !product.IsSoldOut;
    }
}

// 注文リストの行 (数量の増減で金額を替える)
public sealed partial class CartLineItem : ObservableObject
{
    public CartLine Line { get; private set; }

    public Guid Id => Line.Id;

    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public bool IsAfterMeal => Line.Timing == OrderTiming.AfterMeal;

    [ObservableProperty]
    public partial int Quantity { get; set; }

    [ObservableProperty]
    public partial string AmountText { get; set; } = string.Empty;

    public CartLineItem(CartLine line, string name, string optionText)
    {
        Line = line;
        Name = name;
        OptionText = optionText;
        Update(line);
    }

    public void Update(CartLine line)
    {
        Line = line;
        Quantity = line.Quantity;
        AmountText = ViewHelper.Price(line.Amount);
    }
}
