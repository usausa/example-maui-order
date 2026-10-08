namespace TableOrder.TableApp.Modules.Dialogs;

public sealed record MessageParameter(string Title, string Message);

// 知らせ (上限、失敗など)。閉じるだけ
public sealed partial class MessageViewModel : AppDialogViewModelBase, IPopupInitialize<MessageParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public IObserveCommand CloseCommand { get; }

    public MessageViewModel(IPopupNavigator popupNavigator)
    {
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
    }

    public void Initialize(MessageParameter parameter)
    {
        Title = parameter.Title;
        Message = parameter.Message;
    }
}
