namespace TableOrder.Terminal.Table.Models.Order;

// カートの 1 行 (商品、選んだオプション、数量、出す時機)。単価は商品の価格とオプションの差額の和
public sealed record CartLine(
    Guid Id,
    Guid ItemId,
    IReadOnlyList<Guid> OptionIds,
    int Quantity,
    OrderTiming Timing,
    decimal UnitPrice)
{
    public decimal Amount => UnitPrice * Quantity;

    public bool IsSameSelection(Guid itemId, IEnumerable<Guid> optionIds, OrderTiming timing) =>
        (ItemId == itemId) && (Timing == timing) && OptionIds.Order().SequenceEqual(optionIds.Order());
}
