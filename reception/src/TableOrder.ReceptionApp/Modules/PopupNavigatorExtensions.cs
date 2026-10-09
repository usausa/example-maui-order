namespace TableOrder.ReceptionApp.Modules;

// 受付機のポップアップの種類ごとにメソッドを置く (どの端末でも同じもの (電卓、知らせ、確認、PIN) は TableOrder.Terminal の PopupNavigatorExtensions)。null = キャンセル
public static class PopupNavigatorExtensions
{
    // 言語を選ぶ (今の言語に印を付ける)
    public static ValueTask<Language?> LanguageAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.PopupAsync<Language?>(DialogId.Language);
}
