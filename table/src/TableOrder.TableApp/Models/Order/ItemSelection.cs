namespace TableOrder.TableApp.Models.Order;

// 商品の詳細で選んだ内容
public sealed record ItemSelection(
    Guid ItemId,
    IReadOnlyList<Guid> OptionIds,
    int Quantity,
    OrderTiming Timing);
