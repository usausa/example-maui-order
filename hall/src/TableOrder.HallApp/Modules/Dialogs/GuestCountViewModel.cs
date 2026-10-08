namespace TableOrder.HallApp.Modules.Dialogs;

// 表題と添える文 (案内は定員)、はじめの人数、決めるボタンの文言
public sealed record GuestCountParameter(string Title, string Hint, int Adults, int Children, string Ok);

public sealed record GuestCountResult(int Adults, int Children);

// 人数 (大人と子ども) を増減のボタンで入れる。合わせて 1 人以上で決められる (閉じたら null)
public sealed partial class GuestCountViewModel : AppDialogViewModelBase, IPopupInitialize<GuestCountParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Adults { get; set; }

    [ObservableProperty]
    public partial int Children { get; set; }

    public IObserveCommand DecreaseAdultsCommand { get; }

    public IObserveCommand IncreaseAdultsCommand { get; }

    public IObserveCommand DecreaseChildrenCommand { get; }

    public IObserveCommand IncreaseChildrenCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public GuestCountViewModel(IPopupNavigator popupNavigator)
    {
        DecreaseAdultsCommand = MakeDelegateCommand(() => Adults--, () => Adults > 0);
        IncreaseAdultsCommand = MakeDelegateCommand(() => Adults++, () => Adults < Length.MaxGuests);
        DecreaseChildrenCommand = MakeDelegateCommand(() => Children--, () => Children > 0);
        IncreaseChildrenCommand = MakeDelegateCommand(() => Children++, () => Children < Length.MaxGuests);

        // 結果は開く側と同じ型 (GuestCountResult?) で返す
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<GuestCountResult?>(null));
        CommitCommand = MakeAsyncCommand(
            async () => await popupNavigator.CloseAsync<GuestCountResult?>(new GuestCountResult(Adults, Children)),
            () => Adults + Children > 0);
    }

    public void Initialize(GuestCountParameter parameter)
    {
        Title = parameter.Title;
        Hint = parameter.Hint;
        OkText = parameter.Ok;
        Adults = parameter.Adults;
        Children = parameter.Children;
    }
}
