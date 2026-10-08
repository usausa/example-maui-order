namespace TableOrder.Contract.Visits;

// 人数の変更 (ホール端末)。Version は表示していた来店の版
public sealed class VisitUpdateRequest
{
    public int Adults { get; set; }

    public int Children { get; set; }

    public int Version { get; set; }
}
