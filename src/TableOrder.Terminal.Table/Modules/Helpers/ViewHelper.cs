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

    public static string Table(string tableNo) =>
        String.IsNullOrEmpty(tableNo) ? "--" : tableNo;

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

    // 他の言語の名前 (言語の切り替えのボタンに出す)
    public static string SwitchName(Language current) =>
        current == Language.Japanese ? "English" : "日本語";

    //--------------------------------------------------------------------------------
    // Glyph
    //--------------------------------------------------------------------------------

    // 呼び出しの用件の記号 (用件のコードは店舗の設定で決まるので、知らないコードは呼び出しの記号にする)
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

    // 通信の失敗をお客様向けの文言にする
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                "ITEM_SOLD_OUT" or "STOCK_INSUFFICIENT" => AppResources.ErrorSoldOut,
                "CHECKOUT_IN_PROGRESS" => AppResources.ErrorCheckoutInProgress,
                "LIMIT_EXCEEDED" or "QUANTITY_EXCEEDED" => AppResources.ErrorLimit,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
