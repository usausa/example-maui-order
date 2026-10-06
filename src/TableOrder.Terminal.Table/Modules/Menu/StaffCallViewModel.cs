namespace TableOrder.Terminal.Table.Modules.Menu;

// 店員の呼び出し。用件を選んで呼び、開いている間は呼び出しの状態 (向かっています) を読み直す
public sealed partial class StaffCallViewModel : AppDialogViewModelBase
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource cancel = new();

    private readonly ILogger<StaffCallViewModel> log;

    private readonly IOrderApi orderApi;

    private readonly VisitState visitState;

    private readonly Dictionary<string, string> reasonNames;

    // 用件のタイルを 4 つずつ並べる行
    public IReadOnlyList<CallReasonRow> ReasonRows { get; }

    public ObservableCollection<CallStatusItem> Calls { get; } = [];

    [ObservableProperty]
    public partial bool HasCalls { get; set; }

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StaffCallViewModel(
        ILogger<StaffCallViewModel> log,
        IPopupNavigator popupNavigator,
        IOrderApi orderApi,
        MenuState menuState,
        VisitState visitState,
        LanguageState languageState)
    {
        this.log = log;
        this.orderApi = orderApi;
        this.visitState = visitState;

        var language = languageState.Current;
        var reasons = menuState.Config.CallReasons
            .OrderBy(static x => x.SortOrder)
            .Select(x => new CallReasonItem(x.Code, x.Name.Get(language)))
            .ToList();
        ReasonRows = reasons.Chunk(4).Select(static x => new CallReasonRow(x)).ToList();
        reasonNames = reasons.ToDictionary(static x => x.Code, static x => x.Name, StringComparer.Ordinal);

        SelectCommand = MakeAsyncCommand<CallReasonItem>(CallAsync);
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());

        _ = RefreshLoopAsync(cancel.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cancel.Cancel();
            cancel.Dispose();
        }

        base.Dispose(disposing);
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    // 同じ用件で開いている呼び出しがあれば、サーバは新しく作らずにそれを返す
    private async Task CallAsync(CallReasonItem reason)
    {
        IsFailed = false;

        var result = await orderApi.CreateCallAsync(visitState.Id, new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = reason.Code });
        if (result.Content is not { } call)
        {
            log.WarnApiFailed(nameof(IOrderApi.CreateCallAsync), result.Status, result.ErrorCode);
            ErrorText = ViewHelper.ErrorMessage(result);
            IsFailed = true;
            return;
        }

        Apply([call]);
    }

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var result = await orderApi.GetCallsAsync(visitState.Id, token);
            if (result.Content is { } content)
            {
                Apply(content.Items);
            }
            else if (result.Status != ApiStatus.Canceled)
            {
                log.WarnApiFailed(nameof(IOrderApi.GetCallsAsync), result.Status, result.ErrorCode);
            }

            try
            {
                await Task.Delay(RefreshInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // 対応が終わった呼び出しは出さない
    private void Apply(IEnumerable<CallListResponseItem> calls)
    {
        foreach (var call in calls)
        {
            var item = Calls.FirstOrDefault(x => x.Id == call.Id);
            if (call.Status == CallStatus.Done)
            {
                if (item is not null)
                {
                    Calls.Remove(item);
                }
            }
            else if (item is null)
            {
                Calls.Insert(0, new CallStatusItem(call.Id, reasonNames.GetValueOrDefault(call.ReasonCode, call.ReasonCode), call.Status));
            }
            else
            {
                item.Update(call.Status);
            }
        }

        HasCalls = Calls.Count > 0;
    }
}
