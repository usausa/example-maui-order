namespace TableOrder.Domain;

using System.Globalization;

// 営業時間の判定。店舗の現地時刻で比べ、開店の時刻を営業日の区切りにする (日をまたぐ営業も扱う)
public static class StoreHours
{
    private const string TimeFormat = "HH:mm";

    public static TimeOnly Parse(string value) =>
        TimeOnly.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture);

    public static string Format(TimeOnly value) =>
        value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    // 受けた値 (メニューの時間帯) の読み込み。HH:mm でなければ false
    public static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    // 店舗の現地時刻 (タイムゾーンが見つからないときは動いている機器の時刻)
    public static TimeOnly LocalTime(DateTimeOffset now, string timeZone) =>
        TimeOnly.FromDateTime(LocalDateTime(now, timeZone));

    // 営業日。開店の時刻より前は、前の日の営業の続きとして数える
    public static DateOnly BusinessDate(DateTimeOffset now, string timeZone, TimeOnly open)
    {
        var local = LocalDateTime(now, timeZone);
        var date = DateOnly.FromDateTime(local);
        return TimeOnly.FromDateTime(local) < open ? date.AddDays(-1) : date;
    }

    // ラストオーダーまでの残り。開店より前の時刻は前の営業日の続きとして数えるので、閉店後から開店までは負 (過ぎている) になる
    public static TimeSpan UntilLastOrder(TimeOnly now, TimeOnly open, TimeOnly lastOrder) =>
        (lastOrder - open) - (now - open);

    // 時間帯の中か (始まり ≤ 時刻 < 終わり。終わりが始まりより前なら日をまたぐ)。終わりから grace の間も中とする
    // 始まりと終わりが同じ時間帯は空 (どの時刻も外で、猶予も付けない) にする
    public static bool InPeriod(TimeOnly now, TimeOnly start, TimeOnly end, TimeSpan grace = default) =>
        (start != end) && (now - start < (end - start) + grace);

    // 時間帯の終わりまでの残り (時間帯の外は null)
    public static TimeSpan? UntilPeriodEnd(TimeOnly now, TimeOnly start, TimeOnly end) =>
        InPeriod(now, start, end) ? (end - start) - (now - start) : null;

    // ラストオーダーを過ぎた (ラストオーダーのない店は過ぎない)。注文と受付機の来店の開始を断る
    public static bool IsAfterLastOrder(DateTimeOffset now, string timeZone, TimeOnly open, TimeOnly? lastOrder) =>
        (lastOrder is { } last) && (UntilLastOrder(LocalTime(now, timeZone), open, last) < TimeSpan.Zero);

    // 店舗の現地の日時 (タイムゾーンが見つからないときは動いている機器の時刻)
    public static DateTime LocalDateTime(DateTimeOffset now, string timeZone)
    {
        var local = now.ToLocalTime();
        try
        {
            local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return local.DateTime;
    }
}
