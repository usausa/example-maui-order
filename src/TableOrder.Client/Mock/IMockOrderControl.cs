namespace TableOrder.Client.Mock;

// モックのラストオーダー (今の時刻をもとに決める)
public enum MockLastOrder
{
    None,
    Soon,
    Passed
}

// スタッフメニューからモックに障害と進み具合を起こす (画面の失敗の扱いと、注文の進み方を確かめるため)
// 通知を送る操作は、スタッフメニューを閉じてお客様の画面に戻る間をとって、少し待ってから通知を送る
public interface IMockOrderControl
{
    // 操作から通知を送るまでの時間
    TimeSpan EventDelay { get; }

    // true の間は、すべての要求を通信できない (Unavailable) で返す
    bool Offline { get; set; }

    // true の間は、支払を失敗で終える
    bool FailPayments { get; set; }

    // 商品を売り切れにする。売り切れの商品を含む注文は断る
    void SellOut(IEnumerable<Guid> itemIds);

    // 在庫を初めの状態に戻す
    void Restock();

    // 作っている品と提供を待つ品を 1 段進める (食後の品は、お願いがあるまで進めない)
    void AdvanceOrders();

    // ホール端末で来店を開いたことにする (通知 visit.opened)。来店があれば開かずに false
    bool OpenVisit(int adults, int children);

    // レジで払い終えたことにして来店を閉じる (通知 visit.closed)。来店がなければ false
    bool CloseVisit();

    // 注文の一時停止と再開 (通知 store.updated)
    bool OrderingPaused { get; set; }

    // ラストオーダーを、まもなく / 過ぎた / なし にする (通知 store.updated)
    MockLastOrder LastOrder { get; set; }
}
