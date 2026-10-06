namespace TableOrder.Contract.Visits;

// 来店の開始。Id は開いた端末が採番する (送り直しても重複しない)
public sealed class VisitCreateRequest
{
    public Guid Id { get; set; }

    // テーブル端末は省く (端末の置き場所のテーブル)。ホール端末と受付機は案内したテーブル
    public Guid? TableId { get; set; }

    public int Adults { get; set; }

    public int Children { get; set; }
}
