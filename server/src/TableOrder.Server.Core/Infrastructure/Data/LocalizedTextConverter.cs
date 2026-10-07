namespace TableOrder.Server.Core.Infrastructure.Data;

using Smart.Data.Accessor.Converters;

using TableOrder.Server.Core.Infrastructure.Json;

// 言語ごとの文字は JSON の TEXT で保存する (言語が増えても列を足さない)
public sealed class LocalizedTextConverter : IValueConverter<string, LocalizedText>
{
    public static LocalizedText FromDb(string dbValue) =>
        JsonSerializer.Deserialize<LocalizedText>(dbValue, JsonDefaults.Options)!;

    public static string ToDb(LocalizedText clrValue) =>
        JsonSerializer.Serialize(clrValue, JsonDefaults.Options);
}
