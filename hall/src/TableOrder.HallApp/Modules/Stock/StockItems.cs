namespace TableOrder.HallApp.Modules.Stock;

// カテゴリの帯の種類
public enum StockCategoryKind
{
    // 品切れと残りの数のある品だけ
    Limited,
    Category,
    Options
}

// 品切れのタブのカテゴリの帯の 1 つ (品切れ・残り、メニューのカテゴリ、オプション)
public sealed partial class StockCategory : ObservableObject
{
    public StockCategoryKind Kind { get; }

    public string Name { get; }

    // メニューのカテゴリの商品 (ほかの種類は空)
    public IReadOnlyList<Guid> ItemIds { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public StockCategory(StockCategoryKind kind, string name, IReadOnlyList<Guid> itemIds)
    {
        Kind = kind;
        Name = name;
        ItemIds = itemIds;
    }
}

// 品切れにできる品 (商品かオプション。オプションはグループの名前を添える)
public sealed record StockTarget(Guid Id, StockTargetKind Kind, string Name, string Caption);

// 品切れのタブの品。名前と状態 (品切れ、残りの数) を出す (売れる品は状態を出さない)
public sealed partial class StockRow : ObservableObject
{
    public Guid TargetId { get; }

    public StockTargetKind Kind { get; }

    public string Name { get; }

    public string Caption { get; }

    public bool HasCaption => Caption.Length > 0;

    public StockStatus Status { get; private set; }

    public int? Remaining { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSoldOut { get; set; }

    public StockRow(StockTarget target, StockResponseItem? stock)
    {
        TargetId = target.Id;
        Kind = target.Kind;
        Name = target.Name;
        Caption = target.Caption;
        Update(stock);
    }

    public void Update(StockResponseItem? stock)
    {
        Status = stock?.Status ?? StockStatus.Available;
        Remaining = stock?.Remaining;
        StatusText = Status == StockStatus.Available ? string.Empty : ViewHelper.Name(Status, Remaining);
        IsSoldOut = Status == StockStatus.SoldOut;
    }
}
