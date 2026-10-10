namespace TableOrder.ReceptionApp.Modules.Guests;

using TableOrder.ReceptionApp.Modules.Guide;
using TableOrder.Terminal.Components;
using TableOrder.Terminal.Messaging;

// 人数。大人と子ども (小学生以下) を増減のボタンで入れ (合わせて 1 人以上)、席を決めるで来店を開いて案内の画面に進む。合計の人数を出す
// 人数の入る空席がなければ、案内の画面に満席を出す。受付を止めていたら (来店の開き方が替わった)、知らせてから起動からやり直す
// ラストオーダーを過ぎたら (人数を入れている間に過ぎることもある)、送らずに知らせて待受に戻す (サーバも断る)
// しばらく触らなければ待受に戻す (入口で入れかけて離れたお客様の人数を残さず、言語も店舗の初めの言語に戻す)。戻るまでの時間は画面に添える
public sealed partial class GuestsViewModel : AppViewModelBase
{
    // 触らなかったら待受に戻すまでの時間と、それを見る間隔
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger<GuestsViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly IReactiveMessenger messenger;

    private readonly TimeProvider timeProvider;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    private readonly ReceptionUsecase receptionUsecase;

    // 画面を離れたら、触らなかったときに待受に戻すのをやめる
    private readonly CancellationTokenSource watching = new();

    // 最後に触った時刻
    private DateTimeOffset touchedAt;

    // 触らなかったので、開いていた失敗の知らせを閉じた (閉じたあとに時間を数え直さない)
    private bool closedForIdle;

    public BrandMark Brand { get; }

    public string StoreText { get; }

    // 触らずに待受に戻るまでの時間の添え書き
    public string IdleText { get; }

    [ObservableProperty]
    public partial int Adults { get; set; } = 2;

    [ObservableProperty]
    public partial int Children { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; set; }

    public IObserveCommand DecreaseAdultsCommand { get; }

    public IObserveCommand IncreaseAdultsCommand { get; }

    public IObserveCommand DecreaseChildrenCommand { get; }

    public IObserveCommand IncreaseChildrenCommand { get; }

    public IObserveCommand BackCommand { get; }

    public IObserveCommand DecideCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public GuestsViewModel(
        ILogger<GuestsViewModel> log,
        IPopupNavigator popupNavigator,
        IReactiveMessenger messenger,
        TimeProvider timeProvider,
        ImageCache imageCache,
        LanguageState languageState,
        ReceptionState receptionState,
        StoreState storeState,
        ReceptionUsecase receptionUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.messenger = messenger;
        this.timeProvider = timeProvider;
        this.languageState = languageState;
        this.storeState = storeState;
        this.receptionUsecase = receptionUsecase;

        var language = languageState.Current;
        Brand = ViewHelper.Brand(receptionState, imageCache, language);
        StoreText = receptionState.StoreName(language);
        IdleText = ViewHelper.Format(AppResources.GuestsIdleFormat, (int)IdleTimeout.TotalSeconds);
        TotalText = ViewHelper.Total(Adults + Children);
        touchedAt = timeProvider.GetUtcNow();

        DecreaseAdultsCommand = MakeDelegateCommand(() => ChangeGuests(Adults - 1, Children), () => Adults > 0);
        IncreaseAdultsCommand = MakeDelegateCommand(() => ChangeGuests(Adults + 1, Children), () => Adults < Length.MaxGuests);
        DecreaseChildrenCommand = MakeDelegateCommand(() => ChangeGuests(Adults, Children - 1), () => Children > 0);
        IncreaseChildrenCommand = MakeDelegateCommand(() => ChangeGuests(Adults, Children + 1), () => Children < Length.MaxGuests);
        BackCommand = MakeAsyncCommand(BackAsync);
        DecideCommand = MakeAsyncCommand(DecideAsync, () => Adults + Children > 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            watching.Cancel();
            watching.Dispose();
        }

        base.Dispose(disposing);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 人数の画面に入るのは新しいお客様なので、前のお客様の送れたかわからなかった受付は使わない
    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        receptionUsecase.ResetPendingOpen();
        _ = WatchIdleAsync(watching.Token);
        return Task.CompletedTask;
    }

    // 戻るは待受に戻すだけにする (アプリの外へ出さない)
    protected override Task OnNotifyBackAsync() => BackAsync();

    private async Task BackAsync() =>
        await Navigator.ForwardAsync(ViewId.Standby);

    // 触らずにしばらくたったら、お客様が離れたものとして言語を戻して待受に戻す (操作の途中は戻さない)
    // 失敗の知らせを開いたまま離れることもあるので、知らせは閉じ、席を決める操作が終わってから戻す
    private async Task WatchIdleAsync(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await Task.Delay(IdleCheckInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (timeProvider.GetUtcNow() - touchedAt < IdleTimeout)
            {
                continue;
            }

            if (!BusyState.IsBusy)
            {
                break;
            }

            closedForIdle = true;
            messenger.Send(new PopupCloseMessage());
        }

        languageState.Reset();
        await BackAsync();
    }

    //--------------------------------------------------------------------------------
    // Reception
    //--------------------------------------------------------------------------------

    private void ChangeGuests(int adults, int children)
    {
        Adults = adults;
        Children = children;
        TotalText = ViewHelper.Total(adults + children);
        touchedAt = timeProvider.GetUtcNow();
    }

    // 人数を送って来店を開く。決まったテーブルか満席を案内の画面に出す
    private async Task DecideAsync()
    {
        touchedAt = timeProvider.GetUtcNow();
        if (storeState.IsAfterLastOrder(timeProvider.GetUtcNow()))
        {
            await CloseForLastOrderAsync();
            return;
        }

        var result = await receptionUsecase.OpenVisitAsync(Adults, Children);
        if (result.Content is { } visit)
        {
            await Navigator.ForwardAsync(ViewId.Guide, Parameters.MakeGuide(new GuideResult(visit.TableName, visit.Adults, visit.Children)));
            return;
        }

        if (result.ErrorCode == ErrorCodes.NoVacantTable)
        {
            await Navigator.ForwardAsync(ViewId.Guide, Parameters.MakeGuide(new GuideResult(null, Adults, Children)));
            return;
        }

        log.WarnApiFailed(nameof(IReceptionApi.OpenVisitAsync), result.Status, result.ErrorCode);
        if (result.ErrorCode == ErrorCodes.LastOrderPassed)
        {
            await CloseForLastOrderAsync();
            return;
        }

        if (result.ErrorCode == ErrorCodes.VisitOpeningDisabled)
        {
            // 管理画面で来店の開き方を替えた。起動から店舗の設定を読み直す
            await popupNavigator.MessageAsync(AppResources.ErrorTitle, AppResources.StandbyStopped);
            await Navigator.ForwardAsync(ViewId.Startup);
            return;
        }

        await popupNavigator.MessageAsync(AppResources.ErrorTitle, ViewHelper.ErrorMessage(result));
        if (!closedForIdle)
        {
            touchedAt = timeProvider.GetUtcNow();
        }
    }

    // 受付を終えたことを知らせて、言語を戻して待受に戻す (待受は受付の終わりを出す)
    private async Task CloseForLastOrderAsync()
    {
        await popupNavigator.MessageAsync(AppResources.ErrorTitle, AppResources.StandbyClosed);
        languageState.Reset();
        await BackAsync();
    }
}
