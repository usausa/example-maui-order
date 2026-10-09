namespace TableOrder.HallApp.Modules.Dialogs;

// 品の名前と添える文 (オプションはグループの名前)、今の状態
public sealed record StockEditParameter(string Name, string Caption, StockStatus Status, string StatusText);

// 品の状態 (売れる、残りの数を決める、品切れ) を選び、選んだ状態を返す (閉じたら null)。残りの数は開いた側が電卓で入れる
public sealed partial class StockEditViewModel : AppDialogViewModelBase, IPopupInitialize<StockEditParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Caption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    // 今の状態 (選択肢に印を付ける)
    [ObservableProperty]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsLimited { get; set; }

    [ObservableProperty]
    public partial bool IsSoldOut { get; set; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public StockEditViewModel(IPopupNavigator popupNavigator)
    {
        // 結果は開く側と同じ型 (StockStatus?) で返す
        SelectCommand = MakeAsyncCommand<StockStatus>(async x => await popupNavigator.CloseAsync<StockStatus?>(x));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync<StockStatus?>(null));
    }

    public void Initialize(StockEditParameter parameter)
    {
        Title = parameter.Name;
        Caption = parameter.Caption;
        StatusText = parameter.StatusText;
        IsAvailable = parameter.Status == StockStatus.Available;
        IsLimited = parameter.Status == StockStatus.Limited;
        IsSoldOut = parameter.Status == StockStatus.SoldOut;
    }
}
