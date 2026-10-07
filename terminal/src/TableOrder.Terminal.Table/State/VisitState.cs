namespace TableOrder.Terminal.Table.State;

// 今の来店 (人数、答えた確認、注文した品)。会計を終えたら閉じる
public sealed class VisitState
{
    private readonly HashSet<Guid> confirmedRuleIds = [];

    private readonly List<OrderedLine> orderedLines = [];

    public Guid Id { get; private set; }

    public bool IsOpen { get; private set; }

    public int Adults { get; private set; }

    public int Children { get; private set; }

    public int Guests => Adults + Children;

    public VisitStatus Status { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<OrderedLine> OrderedLines => orderedLines;

    public void Open(VisitResponse visit)
    {
        orderedLines.Clear();
        IsOpen = true;
        Update(visit);
    }

    public void Update(VisitResponse visit)
    {
        Id = visit.Id;
        Adults = visit.Adults;
        Children = visit.Children;
        Status = visit.Status;
        Version = visit.Version;

        confirmedRuleIds.Clear();
        confirmedRuleIds.UnionWith(visit.ConfirmedRuleIds);
    }

    public void Close()
    {
        IsOpen = false;
        Id = Guid.Empty;
        Adults = 0;
        Children = 0;
        Status = VisitStatus.Closed;
        confirmedRuleIds.Clear();
        orderedLines.Clear();
    }

    public bool IsConfirmed(Guid ruleId) => confirmedRuleIds.Contains(ruleId);

    // 注文の一覧から、取消を除いた品を数え直す
    public void UpdateOrdered(IEnumerable<OrderListResponseItem> orders)
    {
        orderedLines.Clear();
        orderedLines.AddRange(orders
            .SelectMany(static x => x.Lines)
            .Where(static x => x.Status != OrderLineStatus.Cancelled)
            .Select(static x => new OrderedLine(x.ItemId, x.Options.Select(static o => o.OptionId).ToList(), x.Quantity)));
    }

    public void AddOrdered(OrderListResponseItem order)
    {
        orderedLines.AddRange(order.Lines
            .Where(static x => x.Status != OrderLineStatus.Cancelled)
            .Select(static x => new OrderedLine(x.ItemId, x.Options.Select(static o => o.OptionId).ToList(), x.Quantity)));
    }
}
