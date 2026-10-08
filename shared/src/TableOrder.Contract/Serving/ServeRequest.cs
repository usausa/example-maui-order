namespace TableOrder.Contract.Serving;

// 提供した明細 (ホール端末)
public sealed class ServeRequest
{
    public IReadOnlyList<Guid> LineIds { get; set; } = default!;

    public string? StaffId { get; set; }
}
