namespace TableOrder.ReceptionApp.Modules.Guests;

using TableOrder.ReceptionApp.Modules.Guide;
using TableOrder.Terminal.Components;

// 人数。大人と子ども (小学生以下) を増減のボタンで入れ (合わせて 1 人以上)、席を決めるで来店を開いて案内の画面に進む
// 人数の入る空席がなければ、案内の画面に満席を出す。受付を止めていたら (来店の開き方が替わった)、知らせてから起動からやり直す
// しばらく触らなければ待受に戻す (入口で入れかけて離れたお客様の人数を残さず、言語も店舗の初めの言語に戻す)
public sealed partial class GuestsViewModel : AppViewModelBase
{
    // 触らなかったら待受に戻すまでの時間と、それを見る間隔
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger<GuestsViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly LanguageState languageState;

    private readonly ReceptionUsecase receptionUsecase;

    // 画面を離れたら、触らなかったときに待受に戻すのをやめる
    private readonly CancellationTokenSource watching = new();

    // 最後に触った時刻
    private DateTimeOffset touchedAt;

    public BrandMark Brand { get; }

    [ObservableProperty]
    public partial int Adults { get; set; } = 2;

    [ObservableProperty]
    public partial int Children { get; set; }

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
        TimeProvider timeProvider,
        ImageCache imageCache,
        LanguageState languageState,
        ReceptionState receptionState,
        ReceptionUsecase receptionUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.languageState = languageState;
        this.receptionUsecase = receptionUsecase;

        Brand = ViewHelper.Brand(receptionState, imageCache, languageState.Current);
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

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        _ = WatchIdleAsync(watching.Token);
        return Task.CompletedTask;
    }

    // 戻るは待受に戻すだけにする (アプリの外へ出さない)
    protected override Task OnNotifyBackAsync() => BackAsync();

    private async Task BackAsync() =>
        await Navigator.ForwardAsync(ViewId.Standby);

    // 触らずにしばらくたったら、お客様が離れたものとして言語を戻して待受に戻す (操作の途中は戻さない)
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

            if (!BusyState.IsBusy && (timeProvider.GetUtcNow() - touchedAt >= IdleTimeout))
            {
                break;
            }
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
        touchedAt = timeProvider.GetUtcNow();
    }

    // 人数を送って来店を開く。決まったテーブルか満席を案内の画面に出す
    private async Task DecideAsync()
    {
        touchedAt = timeProvider.GetUtcNow();

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
        if (result.ErrorCode == ErrorCodes.VisitOpeningDisabled)
        {
            // 管理画面で来店の開き方を替えた。起動から店舗の設定を読み直す
            await popupNavigator.MessageAsync(AppResources.ErrorTitle, AppResources.StandbyStopped);
            await Navigator.ForwardAsync(ViewId.Startup);
            return;
        }

        await popupNavigator.MessageAsync(AppResources.ErrorTitle, ViewHelper.ErrorMessage(result));
        touchedAt = timeProvider.GetUtcNow();
    }
}
