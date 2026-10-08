namespace TableOrder.Contract.Orders;

// 明細の取消 (ホール端末)。数量の一部の取消は、取り消す分を別の明細に分ける
public sealed class OrderLineCancelRequest
{
    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public string? StaffId { get; set; }
}
