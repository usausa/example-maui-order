namespace TableOrder.Terminal;

// お客様の画面の言語を替えるときに、アプリの文言 (AppResources) のカルチャを替える処理
public sealed record LanguageOptions(Action<CultureInfo> ApplyCulture);
