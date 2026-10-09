namespace TableOrder.ReceptionApp.Modules.Dialogs;

// 言語の選択肢
public sealed class LanguageChoice
{
    public Language Language { get; }

    public string Name { get; }

    public bool IsSelected { get; }

    public LanguageChoice(Language language, bool isSelected)
    {
        Language = language;
        Name = language.NativeName();
        IsSelected = isSelected;
    }
}
