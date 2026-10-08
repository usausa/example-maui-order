namespace TableOrder.Contract.Calls;

public sealed class CallListResponse
{
    public IReadOnlyList<CallListResponseItem> Items { get; set; } = default!;
}

public sealed class CallListResponseItem
{
    public Guid Id { get; set; }

    public Guid VisitId { get; set; }

    // 来店の今のテーブル (ホールの呼び出しの一覧のため)
    public Guid TableId { get; set; }

    public string TableName { get; set; } = default!;

    public string ReasonCode { get; set; } = default!;

    public CallStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? DoneAt { get; set; }
}
