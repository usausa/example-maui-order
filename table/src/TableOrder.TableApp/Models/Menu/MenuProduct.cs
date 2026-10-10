namespace TableOrder.TableApp.Models.Menu;

// 画面に出す商品 (選んでいる言語の名前に直したもの)。タグで出せる条件を確かめる
public sealed record MenuProduct(
    Guid Id,
    string Name,
    decimal Price,
    string? ImageName,
    IReadOnlyList<ItemBadge> Badges,
    IReadOnlyList<string> Tags,
    bool IsSoldOut);
