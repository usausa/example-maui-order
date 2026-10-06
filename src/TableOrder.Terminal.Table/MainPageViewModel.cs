namespace TableOrder.Terminal.Table;

using TableOrder.Terminal.Table.Modules;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainPageViewModel : ExtendViewModelBase, IAppLifecycle
{
    private readonly IScreen screen;

    private readonly StartupState startup;

    private bool destroying;

    private IDisposable? navigatingBusy;

    public INavigator Navigator { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public MainPageViewModel(
        ILogger<MainPageViewModel> log,
        INavigator navigator,
        IScreen screen,
        StartupState startup)
    {
        Navigator = navigator;
        this.screen = screen;
        this.startup = startup;

        // 遷移の間は Busy にして、画面の操作と戻るを受け付けない
        Disposables.Add(Observable.FromEventPattern<EventArgs>(h => Navigator.ExecutingChanged += h, h => Navigator.ExecutingChanged -= h)
            .Subscribe(_ => UpdateNavigatingBusy()));

        // Screen lock detection
        Disposables.Add(screen.StateChangedAsObservable().ObserveOnCurrentContext().Subscribe(x => log.DebugScreenStateChanged(x.ScreenOn)));
    }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    public async void OnCreated()
    {
        screen.EnableDetectScreenState(true);

        // 画面の配置を決めるまでの仮の全画面 (システムバーを隠す)。専用端末にする段階でロックタスクと合わせて扱いを決め直す
        screen.SetFullscreen(true);

        await startup.Completed;

        // Guard for the case where the Activity is recreated while initialization is still in progress
        if (destroying)
        {
            return;
        }

        Navigator.Exit();
        await Navigator.ForwardAsync(ViewId.Startup);
    }

    public void OnActivated()
    {
    }

    public void OnDeactivated()
    {
    }

    public void OnStopped()
    {
    }

    public void OnResumed()
    {
    }

    public void OnDestroying()
    {
        destroying = true;
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void UpdateNavigatingBusy()
    {
        if (Navigator.Executing)
        {
            navigatingBusy ??= BusyState.Begin();
        }
        else
        {
            navigatingBusy?.Dispose();
            navigatingBusy = null;
        }
    }
}
