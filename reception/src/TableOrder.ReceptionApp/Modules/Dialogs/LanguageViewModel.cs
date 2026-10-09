namespace TableOrder.ReceptionApp.Modules.Dialogs;

// 言語を選ぶ。今の言語に印を付け、選んだ言語を返す (閉じたら null)
public sealed class LanguageViewModel : AppDialogViewModelBase
{
    public IReadOnlyList<LanguageChoice> Languages { get; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public LanguageViewModel(
        IPopupNavigator popupNavigator,
        LanguageState languageState)
    {
        Languages = languageState.Available.Select(x => new LanguageChoice(x, x == languageState.Current)).ToList();

        // 結果は開く側と同じ型 (Language?) で返す (値の型は Nullable と別の型になり、型が違うと結果が渡らない)
        SelectCommand = MakeAsyncCommand<LanguageChoice>(async x => await popupNavigator.CloseAsync<Language?>(x.Language));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
    }
}
