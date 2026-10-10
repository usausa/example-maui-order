namespace TableOrder.TableApp.Modules;

using TableOrder.TableApp.Modules.Menu;
using TableOrder.TableApp.Modules.Standby;

// テーブル端末のポップアップの種類ごとにメソッドを置く (どの端末でも同じもの (電卓、知らせ、確認、PIN) は TableOrder.Terminal の PopupNavigatorExtensions)。null = キャンセル
public static class PopupNavigatorExtensions
{
    //--------------------------------------------------------------------------------
    // 共通
    //--------------------------------------------------------------------------------

    // 言語を選ぶ (今の言語に印を付ける)
    public static ValueTask<Language?> LanguageAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<Language?>(DialogId.Language);

    //--------------------------------------------------------------------------------
    // 待受
    //--------------------------------------------------------------------------------

    // 来店の人数 (来店の開き方が席の店で、お客様が始めるとき)
    public static ValueTask<GuestCountResult?> GuestCountAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<GuestCountResult?>(DialogId.GuestCount);

    //--------------------------------------------------------------------------------
    // 注文
    //--------------------------------------------------------------------------------

    // カートの行を直すときは line に今の内容を渡す
    // 出し分け (availability) で、出せる条件を満たさない商品とオプションを選べなくする
    public static ValueTask<ItemSelection?> ItemDetailAsync(this IPopupNavigator popupNavigator, Guid itemId, MenuAvailability availability, CartLine? line = null) =>
        popupNavigator.PopupAsync<ItemDetailParameter, ItemSelection?>(DialogId.ItemDetail, new ItemDetailParameter(itemId, line, availability));

    // 注文を送れたら true
    public static ValueTask<bool> OrderConfirmAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<bool>(DialogId.OrderConfirm);

    public static ValueTask OrderHistoryAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync(DialogId.OrderHistory);

    public static ValueTask StaffCallAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync(DialogId.StaffCall);
}
