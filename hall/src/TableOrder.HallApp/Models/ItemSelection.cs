namespace TableOrder.HallApp.Models;

// 品の詳細で選んだ内容 (商品、オプション、数量、出す時機)
public sealed record ItemSelection(Guid ItemId, IReadOnlyList<Guid> OptionIds, int Quantity, OrderTiming Timing);
