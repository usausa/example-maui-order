namespace TableOrder.Terminal.Table.Modules.Helpers;

using Fonts;

using TableOrder.Terminal.Table.Components;

// 表示用の書式と文言 (文言は表示する言語の AppResources から引く)
public static class ViewHelper
{
    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    // 税込の価格 (例: ¥1,299)
    public static string Price(decimal value) =>
        $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    // オプションの差額 (例: +¥330)。差額がなければ空
    public static string PriceDelta(decimal value) =>
        value == 0 ? string.Empty : $"+{Price(value)}";

    public static string Format(string format, params object[] args) =>
        String.Format(CultureInfo.CurrentCulture, format, args);

    // テーブルの名前 (割り当てていなければ --)
    public static string Table(string? tableName) =>
        String.IsNullOrEmpty(tableName) ? "--" : tableName;

    public static string Guests(int guests) =>
        Format(AppResources.GuestsFormat, guests);

    public static string Count(int count) =>
        Format(AppResources.CountFormat, count);

    // アプリの版 (例: Version 1.0 (1))
    public static string Version(IAppInfo appInfo) =>
        $"Version {appInfo.VersionString} ({appInfo.BuildString})";

    public static string Time(DateTimeOffset value) =>
        value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    //--------------------------------------------------------------------------------
    // Name
    //--------------------------------------------------------------------------------

    public static string Name(ItemBadge value) =>
        value switch
        {
            ItemBadge.Recommended => AppResources.BadgeRecommended,
            ItemBadge.Popular => AppResources.BadgePopular,
            ItemBadge.New => AppResources.BadgeNew,
            ItemBadge.Limited => AppResources.BadgeLimited,
            _ => value.ToString()
        };

    public static string Name(OrderLineStatus value) =>
        value switch
        {
            OrderLineStatus.Held => AppResources.StatusHeld,
            OrderLineStatus.Ordered => AppResources.StatusOrdered,
            OrderLineStatus.Cooking => AppResources.StatusCooking,
            OrderLineStatus.Ready => AppResources.StatusReady,
            OrderLineStatus.Served => AppResources.StatusServed,
            OrderLineStatus.Cancelled => AppResources.StatusCancelled,
            _ => value.ToString()
        };

    public static string Name(KioskMode value) =>
        value switch
        {
            KioskMode.Managed => AppResources.KioskManaged,
            KioskMode.DeviceOwner => AppResources.KioskDeviceOwner,
            _ => AppResources.KioskNone
        };

    // ヘッダと待受のチェーンの名前と印 (ロゴは保存したもの)
    public static BrandMark Brand(MenuState menuState, ImageCache imageCache, Language language) =>
        new(menuState.BrandName(language), imageCache.PathOf(menuState.LogoImageName));

    // 他の言語の名前 (言語の切り替えのボタンに出す)
    // 言語の名前はその言語で書く (どの言語の画面でも読めるように)
    public static string LanguageName(Language language) =>
        language == Language.English ? "English" : "日本語";

    //--------------------------------------------------------------------------------
    // Glyph
    //--------------------------------------------------------------------------------

    // 呼び出しの用件の記号 (用件のコードは店舗の設定で決まるので、知らないコードは呼び出しの記号にする)
    // 店舗の知らせ (注文の一時停止 / ラストオーダー)
    public static string StoreNoticeGlyph(bool paused) =>
        paused ? MaterialIcons.Pause_circle_outline : MaterialIcons.Schedule;

    public static string CallGlyph(string code) =>
        code switch
        {
            "Staff" => MaterialIcons.Room_service,
            "Water" => MaterialIcons.Water_drop,
            "Plates" => MaterialIcons.Dinner_dining,
            "Cutlery" => MaterialIcons.Flatware,
            "KidsTableware" => MaterialIcons.Child_care,
            "Clear" => MaterialIcons.Cleaning_services,
            "Payment" => MaterialIcons.Payments,
            _ => MaterialIcons.Notifications
        };

    //--------------------------------------------------------------------------------
    // Error
    //--------------------------------------------------------------------------------

    // 通信の失敗をお客様向けの文言にする (端末の登録の失敗は、スタッフが読む端末の設定と起動の画面に出す)
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => result.ErrorCode == "TENANT_SUSPENDED" ? AppResources.ErrorTenantSuspended : AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                "ITEM_SOLD_OUT" or "STOCK_INSUFFICIENT" => AppResources.ErrorSoldOut,
                "CHECKOUT_IN_PROGRESS" => AppResources.ErrorCheckoutInProgress,
                "LIMIT_EXCEEDED" or "QUANTITY_EXCEEDED" => AppResources.ErrorLimit,
                "ORDERING_PAUSED" => AppResources.ErrorOrderingPaused,
                "LAST_ORDER_PASSED" => AppResources.ErrorLastOrderPassed,
                "PAIRING_CODE_INVALID" => AppResources.ErrorPairingCodeInvalid,
                "TENANT_SUSPENDED" => AppResources.ErrorTenantSuspended,
                DeviceUsecase.KindMismatch => AppResources.ErrorDeviceKind,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
