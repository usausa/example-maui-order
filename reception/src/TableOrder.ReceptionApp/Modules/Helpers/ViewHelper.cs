namespace TableOrder.ReceptionApp.Modules.Helpers;

using Fonts;

using TableOrder.ReceptionApp.Modules.Standby;
using TableOrder.Terminal.Components;

// 表示用の書式と文言 (文言は表示する言語の AppResources から引く)
public static class ViewHelper
{
    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    public static string Format(string format, params object[] args) =>
        String.Format(CultureInfo.CurrentCulture, format, args);

    // 人数 (例: 大人 2・子ども 1。子どもがいなければ大人だけ)
    public static string Guests(int adults, int children) =>
        children > 0 ? Format(AppResources.GuestsAdultsChildrenFormat, adults, children) : Format(AppResources.GuestsAdultsFormat, adults);

    // 合計の人数 (例: 合計 3 名)
    public static string Total(int guests) =>
        Format(AppResources.GuestsTotalFormat, guests);

    // アプリの版 (例: Version 1.0 (1))
    public static string Version(IAppInfo appInfo) =>
        $"Version {appInfo.VersionString} ({appInfo.BuildString})";

    //--------------------------------------------------------------------------------
    // Name
    //--------------------------------------------------------------------------------

    // 待受の受付の状態
    public static string Name(StandbyStatus value) =>
        value switch
        {
            StandbyStatus.Available => AppResources.StandbyAvailable,
            StandbyStatus.Full => AppResources.StandbyFull,
            StandbyStatus.Closed => AppResources.StandbyClosed,
            _ => AppResources.StandbyStopped
        };

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
    // Glyph
    //--------------------------------------------------------------------------------

    // 待受の受付の状態の記号 (すぐに案内できる、満席、本日の受付の終了、受付を止めている)
    public static string Glyph(StandbyStatus value) =>
        value switch
        {
            StandbyStatus.Available => MaterialIcons.Event_available,
            StandbyStatus.Full => MaterialIcons.Hourglass_top,
            StandbyStatus.Closed => MaterialIcons.Schedule,
            _ => MaterialIcons.Do_not_disturb_on
        };

    //--------------------------------------------------------------------------------
    // Error
    //--------------------------------------------------------------------------------

    // 通信の失敗をお客様向けの文言にする (端末の登録の失敗は、スタッフが読む端末の設定と起動の画面に出す)
    // 満席と受付を止めたときは、受付の画面が別に扱う。サーバの文言 (Detail) は担当者向けの日本語なので出さず、割り当てのないコードは共通の文言にする
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
                _ => AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
