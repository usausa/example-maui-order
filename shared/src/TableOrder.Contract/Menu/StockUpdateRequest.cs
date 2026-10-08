namespace TableOrder.Contract.Menu;

// 品切れと残りの数の設定 (ホール端末、キッチン端末)。Available は品切れを解く
public sealed class StockUpdateRequest
{
    public StockTargetKind TargetKind { get; set; }

    public StockStatus Status { get; set; }

    // 残りの数 (Limited のときだけ。0 は SoldOut にする)
    public int? Remaining { get; set; }
}
