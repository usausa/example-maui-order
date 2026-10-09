namespace TableOrder.ReceptionApp.Modules.Standby;

using TableOrder.Terminal.Components;

// 待受。お客様が受付するを押すと人数の画面に進む
// 受け付けないとき (来店の開き方が受付機でない店、ラストオーダーの後) と、空席がひとつもないときは、その文言を出して受付するを出さない
// 空席は来店の通知で、ラストオーダーの後かは時刻で出し直す。チェーンと店舗の設定が替わったら (設定の版)、ここで起動からやり直して反映する
public sealed partial class StandbyViewModel : AppViewModelBase
{
    // ラストオーダーの時刻を過ぎたかは時刻で変わるので、しばらくごとに見直す
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<StandbyViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly ImageCache imageCache;

    private readonly StaffLock staffLock;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    private readonly ReceptionState receptionState;

    public BrandMark Brand { get; }

    public string LanguageText { get; }

    public bool HasLanguages { get; }

    public string StoreText { get; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    // 受け付けられる (受付するを出す)
    [ObservableProperty]
    public partial bool CanStart { get; set; }

    public IObserveCommand StartCommand { get; }

    public IObserveCommand LanguageCommand { get; }

    public IObserveCommand StaffCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StandbyViewModel(
        ILogger<StandbyViewModel> log,
        IPopupNavigator popupNavigator,
        TimeProvider timeProvider,
        ImageCache imageCache,
        StaffLock staffLock,
        LanguageState languageState,
        StoreState storeState,
        ReceptionState receptionState)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.imageCache = imageCache;
        this.staffLock = staffLock;
        this.languageState = languageState;
        this.storeState = storeState;
        this.receptionState = receptionState;

        var language = languageState.Current;
        Brand = ViewHelper.Brand(receptionState, imageCache, language);
        LanguageText = language.NativeName();
        HasLanguages = languageState.HasChoice;
        StoreText = receptionState.StoreName(language);

        StartCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.Guests), () => CanStart);
        LanguageCommand = MakeAsyncCommand(SelectLanguageAsync);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);

        Update();

        Disposables.Add(Observable.Interval(CheckInterval).ObserveOnCurrentContext().Subscribe(_ => Update()));
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 設定が替わっていたら起動からやり直す。受け取れなかったロゴがあれば、待たずに取り直す
    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (IsSettingsChanged())
        {
            await Navigator.PostActionAsync(RestartForSettingsAsync);
            return;
        }

        imageCache.RetryInBackground(receptionState.ImageNames);
    }

    // お客様の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    protected override Task OnVacancyChangedAsync()
    {
        Update();
        return Task.CompletedTask;
    }

    // 管理画面でチェーンや店舗の設定を替えたら、起動からやり直して読み直す。ほかの変化 (ラストオーダーの時刻) は出し直す
    protected override Task OnStoreUpdatedAsync()
    {
        if (IsSettingsChanged())
        {
            return RestartForSettingsAsync();
        }

        Update();
        return Task.CompletedTask;
    }

    private bool IsSettingsChanged() =>
        storeState.SettingsVersion != receptionState.Config.SettingsVersion;

    private async Task RestartForSettingsAsync()
    {
        log.InfoSettingsChanged(storeState.SettingsVersion);
        await Navigator.ForwardAsync(ViewId.Startup);
    }

    //--------------------------------------------------------------------------------
    // Reception
    //--------------------------------------------------------------------------------

    private void Update()
    {
        if (!receptionState.IsReceptionStore)
        {
            SetState(AppResources.StandbyStopped, false);
        }
        else if (storeState.IsAfterLastOrder(timeProvider.GetUtcNow()))
        {
            SetState(AppResources.StandbyClosed, false);
        }
        else if (receptionState.VacantTables == 0)
        {
            SetState(AppResources.StandbyFull, false);
        }
        else
        {
            SetState(AppResources.StandbyMessage, true);
        }
    }

    private void SetState(string message, bool canStart)
    {
        Message = message;
        CanStart = canStart;
    }

    // 言語を選び、替えたら文言を引き直すために画面を作り直す
    private async Task SelectLanguageAsync()
    {
        if ((await popupNavigator.LanguageAsync() is not { } language) || (language == languageState.Current))
        {
            return;
        }

        languageState.Change(language);
        await Navigator.ForwardAsync(ViewId.Standby);
    }

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // ブランドの印の長押しで、PIN を確かめてスタッフメニューに入る
    private async Task OpenStaffAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(staffLock))
        {
            await Navigator.ForwardAsync(ViewId.Staff);
        }
    }
}
