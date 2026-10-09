namespace TableOrder.HallApp.Models;

// 代わりの注文のカートの明細。明細の Id は入れたときに決め、送り直しても変えない
public sealed record CartLine(Guid Id, Guid ItemId, IReadOnlyList<Guid> OptionIds, int Quantity, OrderTiming Timing, decimal UnitPrice);
