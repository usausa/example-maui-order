namespace TableOrder.Terminal.Table.Modules;

using TableOrder.Terminal.Table.Modules.Dialogs;
using TableOrder.Terminal.Table.Modules.Menu;
using TableOrder.Terminal.Table.Modules.Standby;

// ポップアップの種類ごとにメソッドを置く (表題と桁数を画面側で持たない)。null = キャンセル
public static class PopupNavigatorExtensions
{
    //--------------------------------------------------------------------------------
    // 共通
    //--------------------------------------------------------------------------------

    public static ValueTask<string?> InputNumberAsync(this IPopupNavigator popupNavigator, string title, string value, int maxLength) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(title, value, maxLength));

    // テーブル番号 (先頭の 0 を残し、空も許す)
    public static ValueTask<string?> InputTableNoAsync(this IPopupNavigator popupNavigator, string value) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(AppResources.SetupTableNo, value, Length.TableNoDigits, digits: true));

    public static ValueTask MessageAsync(this IPopupNavigator popupNavigator, string title, string message) =>
        popupNavigator.PopupAsync(DialogId.Message, new MessageParameter(title, message));

    // 受ける / 断るは同じ大きさのボタンにする
    public static ValueTask<bool> ConfirmAsync(this IPopupNavigator popupNavigator, string title, string message, string ok, string cancel) =>
        popupNavigator.PopupAsync<ConfirmParameter, bool>(DialogId.Confirm, new ConfirmParameter(title, message, ok, cancel));

    //--------------------------------------------------------------------------------
    // 来店
    //--------------------------------------------------------------------------------

    public static ValueTask<GuestCountResult?> GuestCountAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<GuestCountResult?>(DialogId.GuestCount);

    //--------------------------------------------------------------------------------
    // 注文
    //--------------------------------------------------------------------------------

    // カートの行を直すときは line に今の内容を渡す
    public static ValueTask<ItemSelection?> ItemDetailAsync(this IPopupNavigator popupNavigator, Guid itemId, CartLine? line = null) =>
        popupNavigator.PopupAsync<ItemDetailParameter, ItemSelection?>(DialogId.ItemDetail, new ItemDetailParameter(itemId, line));

    // 注文を送れたら true
    public static ValueTask<bool> OrderConfirmAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<bool>(DialogId.OrderConfirm);

    public static ValueTask OrderHistoryAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync(DialogId.OrderHistory);

    public static ValueTask StaffCallAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync(DialogId.StaffCall);
}
