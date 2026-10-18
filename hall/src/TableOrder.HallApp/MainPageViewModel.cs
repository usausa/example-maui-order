namespace TableOrder.HallApp;

using TableOrder.HallApp.Modules;
using TableOrder.Terminal.Components;

// 画面の枠: 画面を切り替える入れ物。起動の準備を待って起動の画面に進み、遷移の間は処理中にして操作を止め、前に出たら専用端末と管理対象の構成を掛け直す
[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed class MainPageViewModel : ExtendViewModelBase, IAppLifecycle
{
    private readonly KioskManager kiosk;

    private readonly ManagedConfiguration managedConfiguration;

    private readonly CallWatch callWatch;

    private readonly StartupState startup;

    private bool destroying;

    private IDisposable? navigatingBusy;

    public INavigator Navigator { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public MainPageViewModel(
        INavigator navigator,
        KioskManager kiosk,
        ManagedConfiguration managedConfiguration,
        CallWatch callWatch,
        StartupState startup)
    {
        Navigator = navigator;
        this.kiosk = kiosk;
        this.managedConfiguration = managedConfiguration;
        this.callWatch = callWatch;
        this.startup = startup;

        // 遷移の間は Busy にして、画面の操作と戻るを受け付けない
        Disposables.Add(Observable.FromEventPattern<EventArgs>(h => Navigator.ExecutingChanged += h, h => Navigator.ExecutingChanged -= h)
            .Subscribe(_ => UpdateNavigatingBusy()));
    }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    public async void OnCreated()
    {
        // 専用端末にする (全画面。Device Owner なら端末の制限も掛ける)。ロックタスクには画面が前に出たときに入る
        kiosk.Initialize();
        kiosk.Resume();

        // 画面が消えている間も呼び出しを知らせる (前景サービスは画面を出している間に始める)
        callWatch.Start();

        await startup.Completed;

        // 初期化の途中で Activity が作り直されたときは進めない
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
        callWatch.Start();
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
