namespace TableOrder.TableApp.Modules.Standby;

using TableOrder.TableApp.Components;

// 待受。来店は案内するスタッフが開き (ホール端末、管理画面の案内)、開いた知らせで注文の画面に進む
// チェーンと店舗の設定が替わったら (設定の版)、来店のないここで起動からやり直して反映する
public sealed class StandbyViewModel : AppViewModelBase
{
    private readonly ILogger<StandbyViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly ImageCache imageCache;

    private readonly StaffLock staffLock;

    private readonly MenuState menuState;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    public BrandMark Brand { get; }

    public string TableText { get; }

    public string LanguageText { get; }

    public bool HasLanguages { get; }

    public IObserveCommand LanguageCommand { get; }

    public IObserveCommand StaffCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StandbyViewModel(
        ILogger<StandbyViewModel> log,
        IPopupNavigator popupNavigator,
        ImageCache imageCache,
        StaffLock staffLock,
        MenuState menuState,
        LanguageState languageState,
        StoreState storeState)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.imageCache = imageCache;
        this.staffLock = staffLock;
        this.menuState = menuState;
        this.languageState = languageState;
        this.storeState = storeState;

        Brand = ViewHelper.Brand(menuState, imageCache, languageState.Current);
        TableText = ViewHelper.Format(AppResources.TableFormat, ViewHelper.Table(menuState.TableName));
        LanguageText = ViewHelper.LanguageName(languageState.Current);
        HasLanguages = languageState.HasChoice;

        LanguageCommand = MakeAsyncCommand(SelectLanguageAsync);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 設定が替わっていたら起動からやり直す。受け取れなかった料理の写真があれば、待たずに取り直す
    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (IsSettingsChanged())
        {
            await Navigator.PostActionAsync(RestartForSettingsAsync);
            return;
        }

        imageCache.RetryInBackground(menuState.ImageNames);
    }

    // お客様の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    // スタッフが来店を開いたら、注文の画面にする
    protected override async Task OnVisitOpenedAsync() =>
        await Navigator.ForwardAsync(ViewId.Menu);

    // 管理画面でチェーンや店舗の設定を替えたら、起動からやり直して読み直す
    protected override Task OnStoreUpdatedAsync() =>
        IsSettingsChanged() ? RestartForSettingsAsync() : Task.CompletedTask;

    private bool IsSettingsChanged() =>
        storeState.SettingsVersion != menuState.Config.SettingsVersion;

    private async Task RestartForSettingsAsync()
    {
        log.InfoSettingsChanged(storeState.SettingsVersion);
        await Navigator.ForwardAsync(ViewId.Startup);
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

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
