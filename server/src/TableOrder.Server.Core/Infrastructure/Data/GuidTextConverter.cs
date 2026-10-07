namespace TableOrder.Server.Core.Infrastructure.Data;

using Smart.Data.Accessor.Converters;

// GUID は小文字のハイフン付き (D 形式) の TEXT で保存する
// (ドライバの既定の書式に任せると大文字と小文字が混ざり、文字列の比較で行を引けなくなる)
public sealed class GuidTextConverter : IValueConverter<string, Guid>
{
    public static Guid FromDb(string dbValue) => Guid.ParseExact(dbValue, "D");

    public static string ToDb(Guid clrValue) => clrValue.ToString("D");
}
