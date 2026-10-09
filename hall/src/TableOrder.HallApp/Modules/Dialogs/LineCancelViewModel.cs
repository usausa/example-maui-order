namespace TableOrder.HallApp.Modules.Dialogs;

// 取り消す明細 (名前、オプション、数量)
public sealed record LineCancelParameter(string Name, string OptionText, int Quantity);

// 明細の取消。数量が 2 以上の明細は取り消す数を選ぶ。取り消す数を返す (閉じたら null)
public sealed partial class LineCancelViewModel : AppDialogViewModelBase, IPopupInitialize<LineCancelParameter>
{
    private int maxQuantity = 1;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OptionText { get; set; } = string.Empty;

    // 数量が 2 以上 (取り消す数を選ぶ)
    [ObservableProperty]
    public partial bool CanChoose { get; set; }

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    public IObserveCommand DecreaseCommand { get; }

    public IObserveCommand IncreaseCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public LineCancelViewModel(IPopupNavigator popupNavigator)
    {
        DecreaseCommand = MakeDelegateCommand(() => Quantity--, () => Quantity > 1);
        IncreaseCommand = MakeDelegateCommand(() => Quantity++, () => Quantity < maxQuantity);

        // 結果は開く側と同じ型 (int?) で返す
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<int?>(null));
        CommitCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<int?>(Quantity));
    }

    // はじめは明細の数量をすべて取り消す
    public void Initialize(LineCancelParameter parameter)
    {
        maxQuantity = parameter.Quantity;
        Name = parameter.Name;
        OptionText = parameter.OptionText;
        CanChoose = parameter.Quantity > 1;
        Quantity = parameter.Quantity;
    }
}
