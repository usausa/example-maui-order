namespace TableOrder.Contract.Visits;

// 来店の開始。Id は開く側 (ホール端末、受付機、管理画面の案内) が採番する (送り直しても重複しない)
public sealed class VisitCreateRequest
{
    public Guid Id { get; set; }

    // 案内したテーブル (省くと入力の誤り)
    public Guid? TableId { get; set; }

    public int Adults { get; set; }

    public int Children { get; set; }
}
