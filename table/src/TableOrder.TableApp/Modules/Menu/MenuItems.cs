namespace TableOrder.TableApp.Modules.Menu;

// カテゴリのタブ (選んでいるタブを主色の面で示す)。カードは出せる条件を満たさないものも含めて持ち、出すときに絞る
public sealed partial class CategoryTab : ObservableObject
{
    public Guid Id { get; }

    public string Name { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<MenuCard> Cards { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public CategoryTab(Guid id, string name, IReadOnlyList<string> tags, IReadOnlyList<MenuCard> cards)
    {
        Id = id;
        Name = name;
        Tags = tags;
        Cards = cards;
    }
}

// メニューのカード (料理の写真、名前、価格、印)。オプションのない商品はカードの + ですぐ入れられる。売り切れは通知で替える
public sealed partial class MenuCard : ObservableObject
{
    private readonly bool hasOptions;

    public Guid Id { get; }

    public string Name { get; }

    public string PriceText { get; }

    public IReadOnlyList<string> Tags { get; }

    // 保存した写真のファイル (保存していなければ null で、代わりにチェーンのロゴか記号を出す)
    public string? ImagePath { get; }

    public string? LogoPath { get; }

    public bool ShowsLogo => (ImagePath is null) && (LogoPath is not null);

    public bool ShowsGlyph => (ImagePath is null) && (LogoPath is null);

    public string BadgeText { get; }

    public bool HasBadge => BadgeText.Length > 0;

    [ObservableProperty]
    public partial bool IsSoldOut { get; set; }

    [ObservableProperty]
    public partial bool CanQuickAdd { get; set; }

    public MenuCard(MenuProduct product, bool hasOptions, string? imagePath, string? logoPath)
    {
        this.hasOptions = hasOptions;
        Id = product.Id;
        Name = product.Name;
        PriceText = ViewHelper.Price(product.Price);
        Tags = product.Tags;
        ImagePath = imagePath;
        LogoPath = logoPath;
        BadgeText = product.Badges.Count > 0 ? ViewHelper.Name(product.Badges[0]) : string.Empty;
        UpdateSoldOut(product.IsSoldOut);
    }

    public void UpdateSoldOut(bool soldOut)
    {
        IsSoldOut = soldOut;
        CanQuickAdd = !hasOptions && !soldOut;
    }
}

// 注文リストの行 (数量の増減で金額を替える)。出せる条件を満たさなくなった行には印を出す
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

    [ObservableProperty]
    public partial bool IsUnavailable { get; set; }

    // 時間外、お子様のみ
    [ObservableProperty]
    public partial string UnavailableText { get; set; } = string.Empty;

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

    public void UpdateAvailability(UnavailableReason reason)
    {
        IsUnavailable = reason != UnavailableReason.None;
        UnavailableText = ViewHelper.UnavailableTag(reason);
    }
}
