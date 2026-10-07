namespace TableOrder.Contract;

// 言語ごとの文字 (日本語は必須。英語がなければ日本語を出す)
public sealed class LocalizedText
{
    public string Ja { get; set; } = default!;

    public string? En { get; set; }
}
