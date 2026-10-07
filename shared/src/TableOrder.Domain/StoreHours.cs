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

    private static DateTime LocalDateTime(DateTimeOffset now, string timeZone)
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
