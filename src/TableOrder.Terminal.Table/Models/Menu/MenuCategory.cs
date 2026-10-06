namespace TableOrder.Terminal.Table.Models.Menu;

// 画面に出すカテゴリ (タブ) と、その中の商品 (表示順)
public sealed record MenuCategory(
    Guid Id,
    string Name,
    IReadOnlyList<MenuProduct> Products);
