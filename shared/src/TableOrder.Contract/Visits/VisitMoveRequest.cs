namespace TableOrder.Contract.Visits;

// テーブルの移動 (ホール端末)
public sealed class VisitMoveRequest
{
    public Guid ToTableId { get; set; }

    public int Version { get; set; }
}
