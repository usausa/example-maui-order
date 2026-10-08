namespace TableOrder.HallApp.Modules.Helpers;

using TableOrder.Terminal.Components;

// 表示用の書式と文言 (文言は端末の言語の AppResources から引く)
public static class ViewHelper
{
    // タブの件数のバッジに出す上限
    private const int MaxBadgeCount = 99;

    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    public static string Format(string format, params object[] args) =>
        String.Format(CultureInfo.CurrentCulture, format, args);

    // アプリの版 (例: Version 1.0 (1))
    public static string Version(IAppInfo appInfo) =>
        $"Version {appInfo.VersionString} ({appInfo.BuildString})";

    // サーバの文字 (店舗の名前など) は端末の言語で選ぶ (英語がなければ日本語)
    public static string Text(LocalizedText text) =>
        (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en") && !String.IsNullOrEmpty(text.En) ? text.En : text.Ja;

    // タブの待っている件数 (なければ空にしてバッジを出さない)
    public static string Badge(int count) =>
        count switch
        {
            <= 0 => string.Empty,
            > MaxBadgeCount => $"{MaxBadgeCount}+",
            _ => count.ToString(CultureInfo.InvariantCulture)
        };

    //--------------------------------------------------------------------------------
    // Name
    //--------------------------------------------------------------------------------

    public static string Name(KioskMode value) =>
        value switch
        {
            KioskMode.Managed => AppResources.KioskManaged,
            KioskMode.DeviceOwner => AppResources.KioskDeviceOwner,
            _ => AppResources.KioskNone
        };

    public static string TabName(ViewId tab) =>
        tab switch
        {
            ViewId.Seats => AppResources.TabSeats,
            ViewId.Calls => AppResources.TabCalls,
            ViewId.Serving => AppResources.TabServing,
            ViewId.Stock => AppResources.TabStock,
            _ => tab.ToString()
        };

    //--------------------------------------------------------------------------------
    // Error
    //--------------------------------------------------------------------------------

    // 通信の失敗をスタッフ向けの文言にする
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => result.ErrorCode == "TENANT_SUSPENDED" ? AppResources.ErrorTenantSuspended : AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                "PAIRING_CODE_INVALID" => AppResources.ErrorPairingCodeInvalid,
                "TENANT_SUSPENDED" => AppResources.ErrorTenantSuspended,
                DeviceUsecase.KindMismatch => AppResources.ErrorDeviceKind,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
