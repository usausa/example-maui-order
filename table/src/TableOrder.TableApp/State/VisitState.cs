namespace TableOrder.TableApp.State;

// 今の来店 (人数、答えた確認、注文した品)。会計を終えたら閉じる
// 来店が終わってから (閉じた、取りやめた、ほかのテーブルに移った) 画面が終えるまでは開いたままにする (お会計はお礼を出してから終える)
public sealed class VisitState
{
    private readonly HashSet<Guid> confirmedRuleIds = [];

    // 注文ごとの、取消を除いた品 (同じ注文を受け直したら置き換え、この端末で送った注文とその通知を重ねて数えない)
    private readonly Dictionary<Guid, List<OrderedLine>> orderedLines = [];

    public Guid Id { get; private set; }

    public bool IsOpen { get; private set; }

    public int Adults { get; private set; }

    public int Children { get; private set; }

    public int Guests => Adults + Children;

    public VisitStatus Status { get; private set; }

    public int Version { get; private set; }

    public IReadOnlyList<OrderedLine> OrderedLines => orderedLines.Values.SelectMany(static x => x).ToList();

    // ほかのテーブルに移った (状態は Open のまま届く)
    public bool IsMoved { get; private set; }

    // 来店が終わった (閉じた、取りやめた、ほかのテーブルに移った)。画面が終えるのを待っている
    public bool IsFinished => IsOpen && (IsMoved || (Status is VisitStatus.Closed or VisitStatus.Cancelled));

    // 今の来店を終える前に、このテーブルで開いた次の来店 (お礼の間に次のお客様を案内した、移ってきた)。今の来店を終えたら開く
    public VisitResponse? Next { get; private set; }

    public void Open(VisitResponse visit)
    {
        orderedLines.Clear();
        IsOpen = true;
        IsMoved = false;
        Next = null;
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
        IsMoved = false;
        Next = null;
        confirmedRuleIds.Clear();
        orderedLines.Clear();
    }

    public void SetMoved() => IsMoved = true;

    // null は次の来店がなくなった (開く前に閉じた、ほかのテーブルに移った)
    public void SetNext(VisitResponse? visit) => Next = visit;

    public bool IsConfirmed(Guid ruleId) => confirmedRuleIds.Contains(ruleId);

    // 注文の一覧から、取消を除いた品を数え直す
    public void UpdateOrdered(IEnumerable<OrderListResponseItem> orders)
    {
        orderedLines.Clear();
        foreach (var order in orders)
        {
            SetOrdered(order);
        }
    }

    // 注文の取消を除いた品を入れる (送った注文、ホール端末で受けた注文、明細の状態が変わった注文。同じ注文は置き換える)
    public void SetOrdered(OrderListResponseItem order) =>
        orderedLines[order.Id] = order.Lines
            .Where(static x => x.Status != OrderLineStatus.Cancelled)
            .Select(static x => new OrderedLine(x.ItemId, x.Options.Select(static o => o.OptionId).ToList(), x.Quantity))
            .ToList();
}
