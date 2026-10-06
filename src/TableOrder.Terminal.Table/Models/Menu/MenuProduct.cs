namespace TableOrder.Terminal.Table.Models.Menu;

// 画面に出す商品 (選んでいる言語の名前に直したもの)
public sealed record MenuProduct(
    Guid Id,
    string Name,
    decimal Price,
    string? ImageName,
    IReadOnlyList<ItemBadge> Badges,
    bool IsSoldOut);
