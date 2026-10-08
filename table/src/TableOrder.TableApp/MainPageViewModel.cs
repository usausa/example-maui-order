namespace TableOrder.TableApp;

using TableOrder.TableApp.Modules;
using TableOrder.Terminal.Components;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainPageViewModel : ExtendViewModelBase, IAppLifecycle
{
    private readonly IScreen screen;

    private readonly KioskManager kiosk;

    private readonly ManagedConfiguration managedConfiguration;

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
        KioskManager kiosk,
        ManagedConfiguration managedConfiguration,
        StartupState startup)
    {
        Navigator = navigator;
        this.screen = screen;
        this.kiosk = kiosk;
        this.managedConfiguration = managedConfiguration;
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

        // 専用端末にする (全画面。Device Owner なら端末の制限も掛ける)。ロックタスクには画面が前に出たときに入る
        kiosk.Initialize();
        kiosk.Resume();

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
        kiosk.Resume();
        managedConfiguration.Refresh();
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
