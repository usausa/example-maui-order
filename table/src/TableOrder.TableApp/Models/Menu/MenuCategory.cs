namespace TableOrder.TableApp.Models.Menu;

// 画面に出すカテゴリ (タブ) と、その中の商品 (表示順)。タグで出せる条件を確かめる
public sealed record MenuCategory(
    Guid Id,
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MenuProduct> Products);
