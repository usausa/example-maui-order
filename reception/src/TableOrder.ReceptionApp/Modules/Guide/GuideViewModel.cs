namespace TableOrder.ReceptionApp.Modules.Guide;

using TableOrder.Terminal.Components;

// 案内。決まったテーブルを大きく出し、人数とお席へお進みくださいを添える。満席のときは、スタッフが案内することを出す
// 閉じるか、しばらくたつと待受に戻る (次のお客様が受付できるように、言語も店舗の初めの言語に戻す)
public sealed partial class GuideViewModel : AppViewModelBase
{
    // 案内を出しておく時間
    private static readonly TimeSpan ShowTime = TimeSpan.FromSeconds(15);

    private readonly LanguageState languageState;

    // 画面を離れたら待受に戻すのをやめる
    private readonly CancellationTokenSource closing = new();

    public BrandMark Brand { get; }

    // 人数の入る空席がなかった
    [ObservableProperty]
    public partial bool IsFull { get; set; }

    [ObservableProperty]
    public partial string TableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GuestsText { get; set; } = string.Empty;

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

        Brand = ViewHelper.Brand(receptionState, imageCache, languageState.Current);

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
            TableText = result.TableName is { } table ? ViewHelper.Table(table) : string.Empty;
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

    // 受付を終えて待受に戻る
    private async Task CloseAsync()
    {
        languageState.Reset();
        await Navigator.ForwardAsync(ViewId.Standby);
    }

    // しばらくたったら待受に戻す。操作の途中 (スタッフメニューの PIN など) なら、その次の機会にする
    private async Task CloseLaterAsync(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await Task.Delay(ShowTime, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!BusyState.IsBusy)
            {
                break;
            }
        }

        await CloseAsync();
    }
}
