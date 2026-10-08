namespace TableOrder.TableApp.Modules.Dialogs;

public sealed partial class InputNumberViewModel : AppDialogViewModelBase, IPopupInitialize<NumberInputParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = default!;

    public NumberInputModel Input { get; } = new();

    public IObserveCommand ClearCommand { get; }
    public IObserveCommand PopCommand { get; }
    public IObserveCommand PushCommand { get; }

    public IObserveCommand CloseCommand { get; }
    public IObserveCommand CommitCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public InputNumberViewModel(IPopupNavigator popupNavigator)
    {
        ClearCommand = MakeDelegateCommand(Input.Clear);
        PopCommand = MakeDelegateCommand(Input.Pop);
        PushCommand = MakeDelegateCommand<string>(Input.Push);

        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
        CommitCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(Input.Text));
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    public void Initialize(NumberInputParameter parameter)
    {
        Title = parameter.Title;
        Input.AllowEmpty = parameter.Digits;
        Input.KeepLeadingZeros = parameter.Digits;
        Input.MaxLength = parameter.MaxLength;
        Input.Masked = parameter.Masked;
        Input.Text = parameter.Value;
    }
}
