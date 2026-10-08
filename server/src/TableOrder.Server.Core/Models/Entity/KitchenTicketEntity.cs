namespace TableOrder.Server.Core.Models.Entity;

// チケット (注文の明細を持ち場ごとに分けたもの)。読み出すときは、テーブルの名前と注文の通し番号を足す
public sealed class KitchenTicketEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid StationId { get; set; }

    public Guid OrderId { get; set; }

    public Guid VisitId { get; set; }

    public KitchenTicketStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DoneAt { get; set; }

    // 読み出しで足す値 (来店の今のテーブル)
    public Guid TableId { get; set; }

    public string TableName { get; set; } = default!;

    public int OrderNo { get; set; }
}
