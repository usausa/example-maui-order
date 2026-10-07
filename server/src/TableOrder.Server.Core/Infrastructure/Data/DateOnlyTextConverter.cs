namespace TableOrder.Server.Core.Infrastructure.Data;

using Smart.Data.Accessor.Converters;

// 日付は yyyy-MM-dd の TEXT で保存する
public sealed class DateOnlyTextConverter : IValueConverter<string, DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public static DateOnly FromDb(string dbValue) => DateOnly.ParseExact(dbValue, Format, CultureInfo.InvariantCulture);

    public static string ToDb(DateOnly clrValue) => clrValue.ToString(Format, CultureInfo.InvariantCulture);
}
