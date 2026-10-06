namespace TableOrder.Client.Mock;

// スタッフメニューからモックに障害と進み具合を起こす (画面の失敗の扱いと、注文の進み方を確かめるため)
public interface IMockOrderControl
{
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
}
