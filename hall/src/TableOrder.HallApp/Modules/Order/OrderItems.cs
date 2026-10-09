namespace TableOrder.HallApp.Modules.Order;

// 代わりの注文のカテゴリの帯の 1 つ (カート、メニューのカテゴリ)
public sealed partial class OrderCategory : ObservableObject
{
    public bool IsCart { get; }

    // メニューのカテゴリの商品 (カートは空)
    public IReadOnlyList<Guid> ItemIds { get; }

    // カートは入れた点数を添える
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public OrderCategory(bool isCart, string name, IReadOnlyList<Guid> itemIds)
    {
        IsCart = isCart;
        Name = name;
        ItemIds = itemIds;
    }
}

// 代わりの注文の画面の行。メニューの品 (価格、品切れと残りの数) か、カートの明細 (オプション、出す時機、数量、金額)
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

    [ObservableProperty]
    public partial bool IsSoldOut { get; set; }

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
        IsSoldOut = status == StockStatus.SoldOut;
    }
}
