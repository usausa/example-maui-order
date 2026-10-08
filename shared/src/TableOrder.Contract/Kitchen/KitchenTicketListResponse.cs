namespace TableOrder.Contract.Kitchen;

// キッチン端末のチケット (注文の明細を持ち場ごとに分けたもの)
public sealed class KitchenTicketListResponse
{
    public IReadOnlyList<KitchenTicketListResponseItem> Items { get; set; } = default!;
}

public sealed class KitchenTicketListResponseItem
{
    public Guid Id { get; set; }

    public Guid StationId { get; set; }

    public Guid OrderId { get; set; }

    public Guid VisitId { get; set; }

    // 来店の今のテーブル
    public string TableName { get; set; } = default!;

    public int OrderNo { get; set; }

    // チケットができた時刻 (食後の品はお願いされた時刻)
    public DateTimeOffset CreatedAt { get; set; }

    public KitchenTicketStatus Status { get; set; }

    public DateTimeOffset? DoneAt { get; set; }

    public IReadOnlyList<KitchenTicketListResponseLine> Lines { get; set; } = default!;
}

// キッチンの表示は日本語の名前だけ (取消した明細も、取消とわかるように出す)
public sealed class KitchenTicketListResponseLine
{
    public Guid LineId { get; set; }

    public string Name { get; set; } = default!;

    public IReadOnlyList<string> Options { get; set; } = default!;

    public int Quantity { get; set; }

    public OrderLineStatus Status { get; set; }
}
