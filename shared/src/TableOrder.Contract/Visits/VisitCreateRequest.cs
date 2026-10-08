namespace TableOrder.Contract.Visits;

// 来店の開始。Id は開く側 (ホール端末、受付機、テーブル端末、管理画面の案内) が採番する (送り直しても重複しない)
public sealed class VisitCreateRequest
{
    public Guid Id { get; set; }

    // 案内したテーブル。ホール端末は必ず送り、受付機は送らない (サーバが空席から決める)。テーブル端末は省くと自分のテーブル
    public Guid? TableId { get; set; }

    public int Adults { get; set; }

    public int Children { get; set; }
}
