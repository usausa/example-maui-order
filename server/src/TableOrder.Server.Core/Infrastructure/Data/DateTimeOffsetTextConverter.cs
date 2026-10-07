namespace TableOrder.Server.Core.Infrastructure.Data;

using Smart.Data.Accessor.Converters;

// 日時は UTC の yyyy-MM-dd HH:mm:ss.fffffff の TEXT で保存する (桁を揃えて、文字列のまま比べて範囲で引けるようにする)
public sealed class DateTimeOffsetTextConverter : IValueConverter<string, DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss.fffffff";

    public static DateTimeOffset FromDb(string dbValue) =>
        DateTimeOffset.ParseExact(dbValue, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public static string ToDb(DateTimeOffset clrValue) =>
        clrValue.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);
}
