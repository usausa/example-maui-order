namespace TableOrder.KitchenApp.Components.Helpers;

using System.Globalization;

// 画面に出す文字の組み立て
public static class ViewHelper
{
    // 値を埋め込む文言 (画面の言語の書式で組み立てる)
    public static string Format(string format, params object?[] args) =>
        String.Format(CultureInfo.CurrentCulture, format, args);

    // サーバの文字 (店舗の名前など) は画面の言語で選ぶ (英語がなければ日本語)
    public static string Text(LocalizedText text) =>
        (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en") && !String.IsNullOrEmpty(text.En) ? text.En : text.Ja;

    // 品の状態 (品切れ、残りの数、売れる)
    public static string StockName(StockStatus status, int? remaining) =>
        status switch
        {
            StockStatus.SoldOut => AppResources.StockSoldOut,
            StockStatus.Limited => Format(AppResources.StockRemainingFormat, remaining ?? 0),
            _ => AppResources.StockAvailable
        };

    // 通信の失敗を画面に出す文言にする
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => result.ErrorCode == ErrorCodes.TenantSuspended ? AppResources.ErrorTenantSuspended : AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                ErrorCodes.PairingCodeInvalid => AppResources.ErrorPairingCodeInvalid,
                ErrorCodes.TenantSuspended => AppResources.ErrorTenantSuspended,
                ErrorCodes.DeviceKindMismatch => AppResources.ErrorDeviceKind,
                KitchenUsecase.KeyUnavailable => AppResources.ErrorDeviceKey,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
