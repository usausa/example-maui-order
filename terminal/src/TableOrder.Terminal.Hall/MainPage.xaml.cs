namespace TableOrder.Terminal.Hall;

using TableOrder.Terminal.Hall.Shell;

public sealed partial class MainPage
{
    private readonly ILogger<MainPage> log;

    public MainPage(ILogger<MainPage> log)
    {
        this.log = log;

        InitializeComponent();
    }

    // 戻るは表示中の画面に任せ、アプリは閉じない
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is MainPageViewModel { BusyState.IsBusy: false } context)
        {
            // 待たずに進めるが、例外はログに残す
            context.Navigator.NotifyAsync(ShellEvent.Back).ContinueWith(
                t => log.WarnUnhandledNavigationError(t.Exception!),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        return true;
    }
}
