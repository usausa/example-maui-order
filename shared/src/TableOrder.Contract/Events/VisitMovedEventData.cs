namespace TableOrder.Contract.Events;

using TableOrder.Contract.Visits;

// visit.moved の中身 (移動した来店と元のテーブル)
public sealed class VisitMovedEventData
{
    public VisitResponse Visit { get; set; } = default!;

    public Guid FromTableId { get; set; }

    public string FromTableName { get; set; } = default!;
}
