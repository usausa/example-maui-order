namespace TableOrder.Contract.Stores;

// 注文の一時停止と再開 (ホール端末)
public sealed class StoreOrderingRequest
{
    public bool Paused { get; set; }

    // 一時停止の間にテーブル端末に出す文言 (再開では使わない)
    public LocalizedText? Message { get; set; }
}
