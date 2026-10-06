namespace TableOrder.Terminal.Table;

using TableOrder.Terminal.Table.Shell;

public sealed partial class MainPage
{
    private readonly ILogger<MainPage> log;

    public MainPage(ILogger<MainPage> log)
    {
        this.log = log;

        InitializeComponent();
    }

    // 戻るは表示中の画面に任せ、アプリは閉じない (お客様の操作で注文の画面から出られないように)
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is MainPageViewModel { BusyState.IsBusy: false } context)
        {
            // 待たずに進めるが、例外はログに残す (観測されないまま次の起動で異常終了として知らせないように)
            context.Navigator.NotifyAsync(ShellEvent.Back).ContinueWith(
                t => log.WarnUnhandledNavigationError(t.Exception!),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        return true;
    }
}
