namespace TableOrder.TableApp.Modules.Helpers;

using Fonts;

using TableOrder.Terminal.Components;

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

    // 時刻は店舗のタイムゾーンで出す (端末のタイムゾーンの設定によらない)
    public static string Time(DateTimeOffset value, string timeZone) =>
        StoreHours.LocalDateTime(value, timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    // カートに入れられないときの知らせ
    public static string LimitMessage(CartLimit limit, MenuState menuState, Language language) =>
        limit switch
        {
            { Kind: CartLimitKind.Lines } => Format(AppResources.MaxLinesFormat, limit.Max),
            { Kind: CartLimitKind.Quantity } => Format(AppResources.MaxQuantityFormat, limit.Max),
            { Rule: { } rule } => Format(AppResources.LimitMessageFormat, menuState.TagName(rule.TargetTag, language)),
            _ => Format(AppResources.StockRemainingFormat, limit.Max)
        };

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

    //--------------------------------------------------------------------------------
    // Glyph
    //--------------------------------------------------------------------------------

    // 注文の知らせ (注文の一時停止 / ラストオーダー)
    public static string OrderNoticeGlyph(bool paused) =>
        paused ? MaterialIcons.Pause_circle_outline : MaterialIcons.Schedule;

    // 会計中の知らせ (お会計のボタンと同じ記号)
    public static string CheckoutNoticeGlyph => MaterialIcons.Payments;

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

    // 通信の失敗をお客様向けの文言にする (端末の登録の失敗は、スタッフが読む端末の設定と起動の画面に出す)
    // サーバの文言 (Detail) は担当者向けの日本語なので出さず、割り当てのないコードは共通の文言にする
    public static string ErrorMessage<T>(ApiResult<T> result) =>
        result.Status switch
        {
            ApiStatus.Unavailable => AppResources.ErrorUnavailable,
            ApiStatus.Unauthorized => result.ErrorCode == ErrorCodes.TenantSuspended ? AppResources.ErrorTenantSuspended : AppResources.ErrorUnauthorized,
            ApiStatus.Rejected => result.ErrorCode switch
            {
                ErrorCodes.ItemSoldOut => AppResources.ErrorSoldOut,
                ErrorCodes.StockInsufficient => AppResources.ErrorStockInsufficient,
                ErrorCodes.LimitExceeded or ErrorCodes.QuantityExceeded => AppResources.ErrorLimit,
                ErrorCodes.ConfirmationRequired => AppResources.ErrorConfirmationRequired,
                ErrorCodes.OptionInvalid => AppResources.ErrorOptionInvalid,
                ErrorCodes.MenuChanged => AppResources.ErrorMenuChanged,
                ErrorCodes.CheckoutInProgress => AppResources.ErrorCheckoutInProgress,
                ErrorCodes.OrderingPaused => AppResources.ErrorOrderingPaused,
                ErrorCodes.LastOrderPassed => AppResources.ErrorLastOrderPassed,
                ErrorCodes.VisitNotOpen => AppResources.ErrorVisitNotOpen,
                ErrorCodes.TableOccupied => AppResources.ErrorTableOccupied,
                ErrorCodes.BillChanged or ErrorCodes.VersionMismatch => AppResources.ErrorBillChanged,
                ErrorCodes.PaymentAmountInvalid => AppResources.ErrorPaymentAmountInvalid,
                ErrorCodes.PaymentMethodUnavailable => AppResources.ErrorPaymentMethodUnavailable,
                ErrorCodes.PairingCodeInvalid => AppResources.ErrorPairingCodeInvalid,
                ErrorCodes.TenantSuspended => AppResources.ErrorTenantSuspended,
                DeviceUsecase.KindMismatch => AppResources.ErrorDeviceKind,
                _ => AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
