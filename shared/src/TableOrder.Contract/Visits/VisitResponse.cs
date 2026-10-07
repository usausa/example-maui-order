namespace TableOrder.Contract.Visits;

// テーブルの開始から会計までの 1 回の来店
public sealed class VisitResponse
{
    public Guid Id { get; set; }

    public string TableName { get; set; } = default!;

    public int Adults { get; set; }

    public int Children { get; set; }

    public VisitStatus Status { get; set; }

    public VisitOpenedBy OpenedBy { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    // 来店で答えた確認のルール
    public IReadOnlyList<Guid> ConfirmedRuleIds { get; set; } = default!;

    // 注文の合計 (取消を除く)
    public decimal OrderTotal { get; set; }

    public int Version { get; set; }
}
