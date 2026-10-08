namespace TableOrder.Terminal.Table.Modules;

using TableOrder.Terminal.Table.Modules.Dialogs;
using TableOrder.Terminal.Table.Modules.Menu;

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

    // ペアリングコード (先頭の 0 を残し、空も許す)
    public static ValueTask<string?> InputPairingCodeAsync(this IPopupNavigator popupNavigator, string value) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(AppResources.SetupPairingCode, value, Length.PairingCodeDigits, digits: true));

    // スタッフの PIN (入れた桁数だけを見せる)
    public static ValueTask<string?> InputPinAsync(this IPopupNavigator popupNavigator, string title) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(title, string.Empty, Length.StaffPinDigits, digits: true, masked: true));

    // スタッフメニュー (と起動に失敗したときの端末の設定) に入る前に PIN を確かめる。違うときと止めているときは知らせて false
    // PIN を受け取る前の端末 (登録していない) は確かめずに入れる
    public static async ValueTask<bool> VerifyStaffAsync(this IPopupNavigator popupNavigator, StaffLock staffLock)
    {
        if (!staffLock.RequiresPin)
        {
            return true;
        }

        if (staffLock.IsLocked)
        {
            await popupNavigator.MessageAsync(AppResources.StaffTitle, AppResources.StaffPinLocked);
            return false;
        }

        if (await popupNavigator.InputPinAsync(AppResources.StaffPin) is not { } pin)
        {
            return false;
        }

        switch (await staffLock.VerifyAsync(pin))
        {
            case StaffPinResult.Accepted:
                return true;
            case StaffPinResult.Locked:
                await popupNavigator.MessageAsync(AppResources.StaffTitle, AppResources.StaffPinLocked);
                return false;
            default:
                await popupNavigator.MessageAsync(AppResources.StaffTitle, AppResources.StaffPinWrong);
                return false;
        }
    }

    public static ValueTask MessageAsync(this IPopupNavigator popupNavigator, string title, string message) =>
        popupNavigator.PopupAsync(DialogId.Message, new MessageParameter(title, message));

    // 受ける / 断るは同じ大きさのボタンにする
    public static ValueTask<bool> ConfirmAsync(this IPopupNavigator popupNavigator, string title, string message, string ok, string cancel) =>
        popupNavigator.PopupAsync<ConfirmParameter, bool>(DialogId.Confirm, new ConfirmParameter(title, message, ok, cancel));

    // 言語を選ぶ (今の言語に印を付ける)
    public static ValueTask<Language?> LanguageAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<Language?>(DialogId.Language);

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
