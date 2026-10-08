namespace TableOrder.Server.Core.Models.Entity;

// 通知。送る先の端末は、種類と、通知が持つテーブル・持ち場で決める
public sealed class EventEntity
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public long Seq { get; set; }

    public string Type { get; set; } = default!;

    public DateTimeOffset OccurredAt { get; set; }

    // 種類ごとの中身の JSON
    public string Data { get; set; } = default!;

    // 通知の先のテーブル (JSON の配列)。null は店舗のすべてのテーブル
    public string? TableIds { get; set; }

    // 通知の先の持ち場。null はすべての持ち場
    public Guid? StationId { get; set; }
}
