namespace TableOrder.Server.Core.Models.Entity;

// テーブルと今の来店の要約 (ホールの席の一覧)。来店がなければ来店の列は null
public sealed class TableSummaryEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Area { get; set; }

    public int Capacity { get; set; }

    public int SortOrder { get; set; }

    public Guid? VisitId { get; set; }

    public int? Adults { get; set; }

    public int? Children { get; set; }

    public VisitStatus? VisitStatus { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public int? VisitVersion { get; set; }

    public DateTimeOffset? LastOrderedAt { get; set; }

    public int UnservedCount { get; set; }

    public int OpenCallCount { get; set; }
}
