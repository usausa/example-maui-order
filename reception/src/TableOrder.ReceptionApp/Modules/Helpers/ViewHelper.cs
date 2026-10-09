namespace TableOrder.ReceptionApp.Modules.Helpers;

using TableOrder.Terminal.Components;

// 表示用の書式と文言 (文言は表示する言語の AppResources から引く)
public static class ViewHelper
{
    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    public static string Format(string format, params object[] args) =>
        String.Format(CultureInfo.CurrentCulture, format, args);

    // 案内するテーブル (例: テーブル 5)
    public static string Table(string tableName) =>
        Format(AppResources.TableFormat, tableName);

    // 人数 (例: 大人 2・子ども 1。子どもがいなければ大人だけ)
    public static string Guests(int adults, int children) =>
        children > 0 ? Format(AppResources.GuestsAdultsChildrenFormat, adults, children) : Format(AppResources.GuestsAdultsFormat, adults);

    // アプリの版 (例: Version 1.0 (1))
    public static string Version(IAppInfo appInfo) =>
        $"Version {appInfo.VersionString} ({appInfo.BuildString})";

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

    // 待受と人数の画面のチェーンの名前と印 (ロゴは保存したもの)
    public static BrandMark Brand(ReceptionState receptionState, ImageCache imageCache, Language language) =>
        new(receptionState.BrandName(language), imageCache.PathOf(receptionState.LogoImageName));

    //--------------------------------------------------------------------------------
    // Error
    //--------------------------------------------------------------------------------

    // 通信の失敗をお客様向けの文言にする (端末の登録の失敗は、スタッフが読む端末の設定と起動の画面に出す)
    // 満席と受付を止めたときは、受付の画面が別に扱う
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => result.ErrorCode == ErrorCodes.TenantSuspended ? AppResources.ErrorTenantSuspended : AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                ErrorCodes.PairingCodeInvalid => AppResources.ErrorPairingCodeInvalid,
                ErrorCodes.TenantSuspended => AppResources.ErrorTenantSuspended,
                DeviceUsecase.KindMismatch => AppResources.ErrorDeviceKind,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
