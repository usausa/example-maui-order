namespace TableOrder.KitchenApp.Components.Pages;

// 品切れの画面のカテゴリの種類 (品切れと残りの数のある品だけ、メニューのカテゴリ、オプション)
public enum StockCategoryKind
{
    Limited,
    Category,
    Options
}

// 品切れの画面のカテゴリの帯の 1 つ
public sealed record StockCategory(StockCategoryKind Kind, string Name, IReadOnlyList<Guid> ItemIds);

// 品切れにできる品かオプション (オプションは組の名前を添える)
public sealed record StockTarget(Guid Id, StockTargetKind Kind, string Name, string Caption);

// 選んだ状態 (残りの数は Limited のときだけ)
public sealed record StockDecision(StockStatus Status, int? Remaining);
