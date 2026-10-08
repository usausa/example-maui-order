namespace TableOrder.Terminal.Modules;

using TableOrder.Terminal.Modules.Dialogs;

// どの端末でも同じポップアップの入口 (表題と桁数を画面側で持たない)。null = キャンセル
public static class PopupNavigatorExtensions
{
    public static ValueTask<string?> InputNumberAsync(this IPopupNavigator popupNavigator, string title, string value, int maxLength) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            TerminalDialogId.InputNumber,
            new NumberInputParameter(title, value, maxLength));

    // ペアリングコード (先頭の 0 を残し、空も許す)
    public static ValueTask<string?> InputPairingCodeAsync(this IPopupNavigator popupNavigator, string value) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            TerminalDialogId.InputNumber,
            new NumberInputParameter(TerminalResources.SetupPairingCode, value, Length.PairingCodeDigits, digits: true));

    // スタッフの PIN (入れた桁数だけを見せる)
    public static ValueTask<string?> InputPinAsync(this IPopupNavigator popupNavigator, string title) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            TerminalDialogId.InputNumber,
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
            await popupNavigator.MessageAsync(TerminalResources.StaffTitle, TerminalResources.StaffPinLocked);
            return false;
        }

        if (await popupNavigator.InputPinAsync(TerminalResources.StaffPin) is not { } pin)
        {
            return false;
        }

        switch (await staffLock.VerifyAsync(pin))
        {
            case StaffPinResult.Accepted:
                return true;
            case StaffPinResult.Locked:
                await popupNavigator.MessageAsync(TerminalResources.StaffTitle, TerminalResources.StaffPinLocked);
                return false;
            default:
                await popupNavigator.MessageAsync(TerminalResources.StaffTitle, TerminalResources.StaffPinWrong);
                return false;
        }
    }

    public static ValueTask MessageAsync(this IPopupNavigator popupNavigator, string title, string message) =>
        popupNavigator.PopupAsync(TerminalDialogId.Message, new MessageParameter(title, message));

    // 受ける / 断るは同じ大きさのボタンにする
    public static ValueTask<bool> ConfirmAsync(this IPopupNavigator popupNavigator, string title, string message, string ok, string cancel) =>
        popupNavigator.PopupAsync<ConfirmParameter, bool>(TerminalDialogId.Confirm, new ConfirmParameter(title, message, ok, cancel));
}
