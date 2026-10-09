namespace TableOrder.HallApp.Modules.Calls;

// 呼び出しのタブ。終わっていない呼び出しを古い順にカードで並べ、向かう・対応したを送る (確かめずに送る)
// カードは通知で読み直した呼び出し (CallState) から出し、呼ばれてからの時間は時間ごとに出し直す
public sealed partial class CallsViewModel : TabViewModelBase
{
    // 呼ばれてからの時間を出し直す間隔
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IPopupNavigator popupNavigator;

    private readonly TimeProvider timeProvider;

    private readonly StoreState storeState;

    private readonly CallState callState;

    private readonly HallUsecase hallUsecase;

    public ObservableCollection<CallItem> Items { get; } = [];

    // 終わっていない呼び出しがない
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public IObserveCommand AcknowledgeCommand { get; }

    public IObserveCommand CompleteCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public CallsViewModel(
        IPopupNavigator popupNavigator,
        TimeProvider timeProvider,
        StoreState storeState,
        CallState callState,
        ServingState servingState,
        HallUsecase hallUsecase)
        : base(ViewId.Calls, storeState, callState, servingState)
    {
        this.popupNavigator = popupNavigator;
        this.timeProvider = timeProvider;
        this.storeState = storeState;
        this.callState = callState;
        this.hallUsecase = hallUsecase;

        AcknowledgeCommand = MakeAsyncCommand<CallItem>(AcknowledgeAsync);
        CompleteCommand = MakeAsyncCommand<CallItem>(CompleteAsync);

        UpdateItems();
        Disposables.Add(Observable.Interval(TickInterval).ObserveOnCurrentContext().Subscribe(_ => Tick()));
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    protected override async Task OnCallsChangedAsync()
    {
        await base.OnCallsChangedAsync();
        UpdateItems();
    }

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    private void UpdateItems()
    {
        var now = timeProvider.GetUtcNow();
        Items.Sync(callState.Items, static (item, call) => item.Id == call.Id, call => new CallItem(call, ReasonText(call), now), (item, call) => item.Update(call, ReasonText(call), now));
        IsEmpty = Items.Count == 0;
    }

    // 店舗の設定にない用件 (あとで外された用件) はコードを出す
    private string ReasonText(CallListResponseItem call) =>
        storeState.FindCallReasonName(call.ReasonCode) is { } name ? ViewHelper.Text(name) : call.ReasonCode;

    private void Tick()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var item in Items)
        {
            item.Tick(now);
        }
    }

    private async Task AcknowledgeAsync(CallItem item) =>
        await AfterOperationAsync(AppResources.CallAcknowledge, await hallUsecase.AcknowledgeCallAsync(item.Id));

    private async Task CompleteAsync(CallItem item) =>
        await AfterOperationAsync(AppResources.CallComplete, await hallUsecase.CompleteCallAsync(item.Id));

    // 読み直した呼び出しを出し (タブの件数も)、断られたら知らせる
    private async Task AfterOperationAsync(string title, ApiResult<CallListResponseItem> result)
    {
        await OnCallsChangedAsync();
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        }
    }
}
