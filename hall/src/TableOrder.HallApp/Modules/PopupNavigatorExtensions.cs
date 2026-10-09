namespace TableOrder.HallApp.Modules;

using TableOrder.HallApp.Modules.Dialogs;

// ホール端末のポップアップの種類ごとにメソッドを置く (どの端末でも同じもの (電卓、知らせ、確認、PIN) は TableOrder.Terminal の PopupNavigatorExtensions)。null = キャンセル
public static class PopupNavigatorExtensions
{
    // 人数 (大人と子ども)。案内と人数の変更で使う
    public static ValueTask<GuestCountResult?> GuestCountAsync(this IPopupNavigator popupNavigator, GuestCountParameter parameter) =>
        popupNavigator.PopupAsync<GuestCountParameter, GuestCountResult?>(DialogId.GuestCount, parameter);

    // 移る席を空いている席から選ぶ (選んだテーブルの id)
    public static ValueTask<Guid?> SelectTableAsync(this IPopupNavigator popupNavigator, MoveTableParameter parameter) =>
        popupNavigator.PopupAsync<MoveTableParameter, Guid?>(DialogId.MoveTable, parameter);

    // 明細の取消 (取り消す数)
    public static ValueTask<int?> LineCancelAsync(this IPopupNavigator popupNavigator, LineCancelParameter parameter) =>
        popupNavigator.PopupAsync<LineCancelParameter, int?>(DialogId.LineCancel, parameter);

    // 会計の明細 (会計を始めるか取りやめるか)
    public static ValueTask<BillAction?> BillAsync(this IPopupNavigator popupNavigator, BillParameter parameter) =>
        popupNavigator.PopupAsync<BillParameter, BillAction?>(DialogId.Bill, parameter);

    // 代わりの注文の品の詳細 (選んだ内容)
    public static ValueTask<ItemSelection?> OrderItemAsync(this IPopupNavigator popupNavigator, OrderItemParameter parameter) =>
        popupNavigator.PopupAsync<OrderItemParameter, ItemSelection?>(DialogId.OrderItem, parameter);

    // 品の状態 (売れる、残りの数を決める、品切れ) を選ぶ
    public static ValueTask<StockStatus?> StockEditAsync(this IPopupNavigator popupNavigator, StockEditParameter parameter) =>
        popupNavigator.PopupAsync<StockEditParameter, StockStatus?>(DialogId.StockEdit, parameter);
}
