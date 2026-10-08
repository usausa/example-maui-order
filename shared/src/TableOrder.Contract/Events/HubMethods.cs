namespace TableOrder.Contract.Events;

// 通知のハブ (/hubs/store) で、サーバが端末に送るメソッドの名前
public static class HubMethods
{
    // 通知 (引数は EventListResponseItem)
    public const string Event = "event";

    // 端末をグループに入れ終えた (引数は店舗の今の通し番号)。端末はこれを受けてから GET /events で抜けた通知を読む
    // はじめてつないだ端末は、この番号から数える (このあとに今の状態を読む)
    public const string Ready = "ready";
}
