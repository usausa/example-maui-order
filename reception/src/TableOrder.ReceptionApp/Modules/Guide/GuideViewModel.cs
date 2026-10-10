namespace TableOrder.ReceptionApp.Modules.Guide;

using TableOrder.Terminal.Components;

// 案内。決まったテーブルを大きく出し、人数とお席へお進みくださいを添える。満席のときは、スタッフが案内することを出す
// 閉じるか、しばらくたつと待受に戻る (次のお客様が受付できるように、言語も店舗の初めの言語に戻す)。戻るまでの残りの秒を出す
// 起動からやり直す知らせ (端末を替えた、長く切れていた) は、案内を消さないように印だけ付けて、閉じたときに待受ではなく起動に移る
public sealed partial class GuideViewModel : AppViewModelBase
{
    // 案内を出しておく秒
    private const int ShowSeconds = 15;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly LanguageState languageState;

    // 画面を離れたら待受に戻すのをやめる
    private readonly CancellationTokenSource closing = new();

    // 起動からやり直す知らせを受けた (閉じたら起動に移る)
    private bool restartRequested;

    public BrandMark Brand { get; }

    public string StoreText { get; }

    // 人数の入る空席がなかった
    [ObservableProperty]
    public partial bool IsFull { get; set; }

    // 案内するテーブルの名前 (札に大きく出す)
    [ObservableProperty]
    public partial string TableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GuestsText { get; set; } = string.Empty;

    // 待受に戻るまでの残り (帯の長さの割合と文言)
    [ObservableProperty]
    public partial double RemainingRatio { get; set; } = 1;

    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;

    public IObserveCommand CloseCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public GuideViewModel(
        ImageCache imageCache,
        LanguageState languageState,
        ReceptionState receptionState)
    {
        this.languageState = languageState;

        var language = languageState.Current;
        Brand = ViewHelper.Brand(receptionState, imageCache, language);
        StoreText = receptionState.StoreName(language);
        UpdateRemaining(ShowSeconds);

        CloseCommand = MakeAsyncCommand(CloseAsync);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closing.Cancel();
            closing.Dispose();
        }

        base.Dispose(disposing);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    public override Task OnNavigatingToAsync(INavigationContext context)
    {
        if (context.Parameter.GetGuide() is { } result)
        {
            IsFull = result.TableName is null;
            TableText = result.TableName ?? string.Empty;
            GuestsText = ViewHelper.Guests(result.Adults, result.Children);
        }

        return Task.CompletedTask;
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        _ = CloseLaterAsync(closing.Token);
        return Task.CompletedTask;
    }

    // 戻るは閉じると同じにする (アプリの外へ出さない)
    protected override Task OnNotifyBackAsync() => CloseAsync();

    protected override Task OnRestartAsync()
    {
        restartRequested = true;
        return Task.CompletedTask;
    }

    // 受付を終えて待受に戻る (起動からやり直す知らせを受けていたら起動に移る)
    private async Task CloseAsync()
    {
        languageState.Reset();
        await Navigator.ForwardAsync(restartRequested ? ViewId.Startup : ViewId.Standby);
    }

    // 残りの秒を数えて出し、なくなったら待受に戻す。閉じるを押している途中 (遷移の間) なら、終わってから戻す
    private async Task CloseLaterAsync(CancellationToken token)
    {
        var remaining = ShowSeconds;
        while ((remaining > 0) || BusyState.IsBusy)
        {
            try
            {
                await Task.Delay(TickInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            remaining = Math.Max(remaining - 1, 0);
            UpdateRemaining(remaining);
        }

        await CloseAsync();
    }

    private void UpdateRemaining(int seconds)
    {
        RemainingRatio = (double)seconds / ShowSeconds;
        RemainingText = ViewHelper.Format(AppResources.GuideCountdownFormat, seconds);
    }
}
