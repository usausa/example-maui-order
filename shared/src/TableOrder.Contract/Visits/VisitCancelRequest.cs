namespace TableOrder.Contract.Visits;

// 注文のないまま帰った来店の取りやめ (ホール端末)
public sealed class VisitCancelRequest
{
    public int Version { get; set; }
}
