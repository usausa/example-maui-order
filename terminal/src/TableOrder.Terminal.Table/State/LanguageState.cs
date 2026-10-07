namespace TableOrder.Terminal.Table.State;

// 画面の言語。画面の文言 (AppResources) は言語のカルチャで引き、金額と時刻の書式は変えない
public sealed class LanguageState
{
    private static readonly CultureInfo JapaneseCulture = new("ja-JP");

    private static readonly CultureInfo EnglishCulture = new("en-US");

    public Language Current { get; private set; } = Language.Japanese;

    public void Change(Language language)
    {
        Current = language;

        var culture = language == Language.English ? EnglishCulture : JapaneseCulture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        AppResources.Culture = culture;
    }

    // 端末の言語の設定によらず日本語から始める
    public void Reset() => Change(Language.Japanese);
}
