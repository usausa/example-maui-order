namespace TableOrder.Terminal.Modules.Dialogs;

public sealed record ConfirmParameter(string Title, string Message, string Ok, string Cancel);

// 確認: 年齢の確認や、注文していない商品があるときのお会計などを確かめる。受けたら true
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

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public ConfirmViewModel()
    {
        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
        CommitCommand = MakeAsyncCommand(async () => await CloseAsync(true));
    }

    public void Initialize(ConfirmParameter parameter)
    {
        Title = parameter.Title;
        Message = parameter.Message;
        OkText = parameter.Ok;
        CancelText = parameter.Cancel;
    }
}
