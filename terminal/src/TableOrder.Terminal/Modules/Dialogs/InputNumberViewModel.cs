namespace TableOrder.Terminal.Modules.Dialogs;

// 電卓: 数と番号 (PIN、ペアリングコード) を入れ、入れた文字を返す (閉じたら null)
public sealed partial class InputNumberViewModel : AppDialogViewModelBase, IPopupInitialize<NumberInputParameter>, IPopupInitialize<DigitsInputParameter>
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

    public InputNumberViewModel()
    {
        ClearCommand = MakeDelegateCommand(Input.Clear);
        PopCommand = MakeDelegateCommand(Input.Pop);
        PushCommand = MakeDelegateCommand<string>(Input.Push);

        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
        CommitCommand = MakeAsyncCommand(async () => await CloseAsync(Input.Text));
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    public void Initialize(NumberInputParameter parameter)
    {
        Title = parameter.Title;
        Input.MaxLength = parameter.MaxLength;
        Input.Text = parameter.Value;
    }

    public void Initialize(DigitsInputParameter parameter)
    {
        Title = parameter.Title;
        Input.AllowEmpty = true;
        Input.KeepLeadingZeros = true;
        Input.MaxLength = parameter.MaxLength;
        Input.Masked = parameter.Masked;
        Input.Text = parameter.Value;
    }
}
