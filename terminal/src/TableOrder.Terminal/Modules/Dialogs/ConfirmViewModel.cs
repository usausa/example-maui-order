namespace TableOrder.Terminal.Modules.Dialogs;

public sealed record ConfirmParameter(string Title, string Message, string Ok, string Cancel);

// 確かめる (年齢の確認、注文していない商品があるときのお会計など)。受けたら true
public sealed partial class ConfirmViewModel : AppDialogViewModelBase, IPopupInitialize<ConfirmParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OkText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CancelText { get; set; } = string.Empty;

    public IObserveCommand OkCommand { get; }

    public IObserveCommand CancelCommand { get; }

    public ConfirmViewModel(IPopupNavigator popupNavigator)
    {
        OkCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(true));
        CancelCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(false));
    }

    public void Initialize(ConfirmParameter parameter)
    {
        Title = parameter.Title;
        Message = parameter.Message;
        OkText = parameter.Ok;
        CancelText = parameter.Cancel;
    }
}
