namespace TableOrder.Contract.Events;

using System.Text.Json;

// 抜けた通知 (GET /events?after=)。ハブで送る通知も同じ形 (EventListResponseItem)
public sealed class EventListResponse
{
    // 店舗の今の通し番号 (端末は読んだ通知のあと、この端末に送らない通知の番号もここまで進める)
    public long LastSeq { get; set; }

    public IReadOnlyList<EventListResponseItem> Items { get; set; } = default!;
}

public sealed class EventListResponseItem
{
    // 店舗の中の通し番号
    public long Seq { get; set; }

    public string Type { get; set; } = default!;

    public DateTimeOffset OccurredAt { get; set; }

    // 種類ごとの中身 (来店の通知は VisitResponse、店舗は StoreResponse など)
    public JsonElement Data { get; set; }
}
