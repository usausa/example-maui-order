namespace TableOrder.Terminal.Table.Models.Order;

// 来店で注文した品 (ルールの数を数えるため。取消は除く)
public sealed record OrderedLine(
    Guid ItemId,
    IReadOnlyList<Guid> OptionIds,
    int Quantity);
