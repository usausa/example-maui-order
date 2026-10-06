namespace TableOrder.Terminal.Table.Models;

// 画面の言語 (お客様が切り替え、来店が終わると日本語に戻す)
public enum Language
{
    Japanese,
    English
}

public static class LanguageExtensions
{
    // 英語がなければ日本語を出す
    public static string Get(this LocalizedText text, Language language) =>
        (language == Language.English) && !String.IsNullOrEmpty(text.En) ? text.En : text.Ja;

    public static string? Get(this LocalizedText? text, Language language, string? fallback) =>
        text is null ? fallback : text.Get(language);
}
