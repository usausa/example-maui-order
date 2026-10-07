namespace TableOrder.Server.Core.Infrastructure.Json;

using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

// API の応答と、DB に JSON で持つ値 (公開されたメニュー、言語ごとの文字) の形を揃える
// camelCase、null は省く、列挙型は名前、日時は UTC
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DateTimeOffsetConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions();
        Apply(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
