namespace TableOrder.Terminal.Modules.Dialogs;

public sealed record MessageParameter(string Title, string Message);

// 知らせ: 上限や失敗などを知らせ、閉じるだけ
public sealed partial class MessageViewModel : AppDialogViewModelBase, IPopupInitialize<MessageParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public IObserveCommand CloseCommand { get; }

    public MessageViewModel()
    {
        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
    }

    public void Initialize(MessageParameter parameter)
    {
        Title = parameter.Title;
        Message = parameter.Message;
    }
}
