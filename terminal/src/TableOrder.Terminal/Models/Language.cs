namespace TableOrder.Terminal.Models;

// お客様の画面の言語 (お客様が切り替え、来店や受付が終わると店舗の初めの言語に戻す)
public enum Language
{
    Japanese,
    English
}

public static class LanguageExtensions
{
    // 店舗の設定の言語のコード ("ja"、"en")。知らないコードは null
    public static Language? FromCode(string code) =>
        code switch
        {
            "ja" => Language.Japanese,
            "en" => Language.English,
            _ => null
        };

    // 言語の名前。どの言語の画面でも読めるように、その言語で書く
    public static string NativeName(this Language language) =>
        language == Language.English ? "English" : "日本語";

    // 英語がなければ日本語を出す
    public static string Get(this LocalizedText text, Language language) =>
        (language == Language.English) && !String.IsNullOrEmpty(text.En) ? text.En : text.Ja;

    public static string? Get(this LocalizedText? text, Language language, string? fallback) =>
        text is null ? fallback : text.Get(language);
}
