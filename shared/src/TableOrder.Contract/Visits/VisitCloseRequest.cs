namespace TableOrder.Contract.Visits;

// テーブルの外で会計した来店を終える (ホール端末、POS)。テーブルで払い終えたときはサーバが閉じる
public sealed class VisitCloseRequest
{
    // Register (レジで払った) か Hall (スタッフが閉じた)
    public VisitClosedBy ClosedBy { get; set; }

    public int Version { get; set; }

    public string? StaffId { get; set; }
}
