namespace TableOrder.Contract.Calls;

public sealed class CallListResponse
{
    public IReadOnlyList<CallListResponseItem> Items { get; set; } = default!;
}

public sealed class CallListResponseItem
{
    public Guid Id { get; set; }

    public string ReasonCode { get; set; } = default!;

    public CallStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }
}
