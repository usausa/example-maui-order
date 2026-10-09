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

    // 品の状態 (売れる、残りの数を決める、品切れ) を選ぶ
    public static ValueTask<StockStatus?> StockEditAsync(this IPopupNavigator popupNavigator, StockEditParameter parameter) =>
        popupNavigator.PopupAsync<StockEditParameter, StockStatus?>(DialogId.StockEdit, parameter);
}
