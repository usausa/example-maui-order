namespace TableOrder.HallApp.Modules.Helpers;

using TableOrder.Terminal.Components;

// 表示用の書式と文言 (文言は端末の言語の AppResources から引く)
public static class ViewHelper
{
    // タブの件数のバッジに出す上限
    private const int MaxBadgeCount = 99;

    private const int MinutesPerHour = 60;

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

    // 税込の価格 (例: ¥1,299)
    public static string Price(decimal value) =>
        $"¥{value.ToString("#,0", CultureInfo.InvariantCulture)}";

    // テーブル (例: テーブル 3)
    public static string Table(string name) =>
        Format(AppResources.TableFormat, name);

    // 合わせた人数 (席のタイル)
    public static string Guests(int adults, int children) =>
        Format(AppResources.GuestsFormat, adults + children);

    // 人数の内訳 (子どもがいなければ大人だけ)
    public static string GuestDetail(int adults, int children) =>
        children > 0 ? Format(AppResources.GuestDetailFormat, adults, children) : Format(AppResources.GuestAdultsFormat, adults);

    // 経過時間 (分に切り捨てる。1 時間を超えたら時間と分)
    public static string Elapsed(TimeSpan value)
    {
        var minutes = Math.Max(0, (int)value.TotalMinutes);
        return minutes < MinutesPerHour
            ? Format(AppResources.ElapsedMinutesFormat, minutes)
            : Format(AppResources.ElapsedHoursFormat, minutes / MinutesPerHour, minutes % MinutesPerHour);
    }

    // 店舗の現地の時刻 (端末のタイムゾーンの設定によらない)
    public static string Time(DateTimeOffset value, string timeZone) =>
        StoreHours.LocalDateTime(value, timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

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

    // 席の状態 (来店がなければ空き)
    public static string SeatStatus(VisitStatus? value) =>
        value switch
        {
            null => AppResources.SeatVacant,
            VisitStatus.Paying => AppResources.SeatPaying,
            _ => AppResources.SeatOpen
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

    //--------------------------------------------------------------------------------
    // Error
    //--------------------------------------------------------------------------------

    // 通信の失敗をスタッフ向けの文言にする
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
                ErrorCodes.TableOccupied => AppResources.ErrorTableOccupied,
                ErrorCodes.VersionMismatch => AppResources.ErrorVersionMismatch,
                ErrorCodes.VisitHasOrders => AppResources.ErrorVisitHasOrders,
                ErrorCodes.CheckoutInProgress => AppResources.ErrorCheckoutInProgress,
                ErrorCodes.VisitNotOpen => AppResources.ErrorVisitNotOpen,
                ErrorCodes.LineStatusInvalid => AppResources.ErrorLineStatusInvalid,
                ErrorCodes.NotFound => AppResources.ErrorNotFound,
                _ => result.Detail ?? AppResources.ErrorGeneric
            },
            _ => AppResources.ErrorGeneric
        };
}
