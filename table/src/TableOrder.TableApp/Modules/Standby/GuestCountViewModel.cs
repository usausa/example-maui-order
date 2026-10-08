namespace TableOrder.TableApp.Modules.Standby;

public sealed record GuestCountResult(int Adults, int Children);

// 来店の人数 (大人と子ども)。合わせて 1 人以上で始められる (閉じたら null)
public sealed partial class GuestCountViewModel : AppDialogViewModelBase
{
    [ObservableProperty]
    public partial int Adults { get; set; } = 2;

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

        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<GuestCountResult?>(null));
        CommitCommand = MakeAsyncCommand(
            async () => await popupNavigator.CloseAsync<GuestCountResult?>(new GuestCountResult(Adults, Children)),
            () => Adults + Children > 0);
    }
}
