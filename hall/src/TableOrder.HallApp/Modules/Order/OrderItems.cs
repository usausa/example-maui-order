namespace TableOrder.HallApp.Modules.Order;

// 代わりの注文のカテゴリの帯の 1 つ (カート、メニューのカテゴリ)
public sealed partial class OrderCategory : ObservableObject
{
    public bool IsCart { get; }

    // メニューのカテゴリの商品 (カートは空)
    public IReadOnlyList<Guid> ItemIds { get; }

    // 出せる条件を確かめるタグ (カートは空)
    public IReadOnlyList<string> Tags { get; }

    // カートは入れた点数を添える
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public OrderCategory(bool isCart, string name, IReadOnlyList<Guid> itemIds, IReadOnlyList<string> tags)
    {
        IsCart = isCart;
        Name = name;
        ItemIds = itemIds;
        Tags = tags;
    }
}

// 代わりの注文の画面の行。メニューの品 (価格、品切れと残りの数) か、カートの明細 (オプション、出す時機、数量、金額、出せる条件を満たさない印)
public sealed partial class OrderRow : ObservableObject
{
    // メニューの品は商品の Id、カートの明細は明細の Id
    public Guid Id { get; }

    public bool IsCartLine { get; }

    public string Name { get; }

    public string Caption { get; }

    public bool HasCaption => Caption.Length > 0;

    public string PriceText { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    // 入れられない (品切れ、出せる条件を満たさないカートの明細)。状態の文言を失敗の色にする
    [ObservableProperty]
    public partial bool IsBlocked { get; set; }

    private OrderRow(Guid id, bool isCartLine, string name, string caption, string priceText)
    {
        Id = id;
        IsCartLine = isCartLine;
        Name = name;
        Caption = caption;
        PriceText = priceText;
    }

    public static OrderRow FromItem(MenuResponseItem item, StockResponseItem? stock)
    {
        var row = new OrderRow(item.Id, false, ViewHelper.Text(item.Name), string.Empty, ViewHelper.Price(item.Price));
        row.UpdateStock(stock);
        return row;
    }

    public static OrderRow FromCart(CartLine line, string name, string caption) =>
        new(line.Id, true, name, caption, $"× {line.Quantity}  {ViewHelper.Price(line.UnitPrice * line.Quantity)}");

    // 品切れと残りの数 (カートの明細は出さない)
    public void UpdateStock(StockResponseItem? stock)
    {
        if (IsCartLine)
        {
            return;
        }

        var status = stock?.Status ?? StockStatus.Available;
        StatusText = status == StockStatus.Available ? string.Empty : ViewHelper.Name(status, stock?.Remaining);
        IsBlocked = status == StockStatus.SoldOut;
    }

    // カートの明細が出せる条件を満たさない印 (時間外、お子様のみ)
    public void UpdateAvailability(UnavailableReason reason)
    {
        if (!IsCartLine)
        {
            return;
        }

        StatusText = ViewHelper.UnavailableTag(reason);
        IsBlocked = reason != UnavailableReason.None;
    }
}
