namespace TableOrder.TableApp.State;

// 画面の言語。画面の文言 (AppResources) は言語のカルチャで引き、金額と時刻の書式は変えない
public sealed class LanguageState
{
    private static readonly CultureInfo JapaneseCulture = new("ja-JP");

    private static readonly CultureInfo EnglishCulture = new("en-US");

    public Language Current { get; private set; } = Language.Japanese;

    // 店舗で選べる言語 (店舗の設定の順。初めの言語に戻す)
    public IReadOnlyList<Language> Available { get; private set; } = Enum.GetValues<Language>();

    // 選べる言語が 2 つ以上 (1 つなら言語のボタンを出さない)
    public bool HasChoice => Available.Count > 1;

    public void Change(Language language)
    {
        Current = language;

        var culture = language == Language.English ? EnglishCulture : JapaneseCulture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        AppResources.Culture = culture;
    }

    // 店舗の設定の言語にする。今の言語を選べなければ初めの言語に戻す
    public void SetAvailable(IReadOnlyList<Language> languages)
    {
        Available = languages.Count > 0 ? languages : [Language.Japanese];
        if (!Available.Contains(Current))
        {
            Reset();
        }
    }

    // 端末の言語の設定によらず、店舗の初めの言語から始める
    public void Reset() => Change(Available[0]);
}
