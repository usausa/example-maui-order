namespace TableOrder.Terminal.Table.Modules.Menu;

// 店員の呼び出し。用件を選んで呼び、開いている間は呼び出しの状態 (向かっています) を読み直す
public sealed partial class StaffCallViewModel : AppDialogViewModelBase
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource cancel = new();

    private readonly ILogger<StaffCallViewModel> log;

    private readonly ITableApi tableApi;

    private readonly VisitState visitState;

    private readonly Dictionary<string, string> reasonNames;

    // 操作 (呼び出し) で内容を反映するたびに進める。読み直しの結果は、頼んだあとに操作で反映していたら古いので使わない
    private int revision;

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
        ITableApi tableApi,
        MenuState menuState,
        VisitState visitState,
        LanguageState languageState)
    {
        this.log = log;
        this.tableApi = tableApi;
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

        var result = await tableApi.CreateCallAsync(visitState.Id, new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = reason.Code });
        if (result.Content is not { } call)
        {
            log.WarnApiFailed(nameof(ITableApi.CreateCallAsync), result.Status, result.ErrorCode);
            ErrorText = ViewHelper.ErrorMessage(result);
            IsFailed = true;
            return;
        }

        revision++;
        Apply([call]);
    }

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var requested = revision;
            var result = await tableApi.GetCallsAsync(visitState.Id, token);
            if (result.Content is { } content)
            {
                if (requested == revision)
                {
                    Apply(content.Items);
                }
            }
            else if (result.Status != ApiStatus.Canceled)
            {
                log.WarnApiFailed(nameof(ITableApi.GetCallsAsync), result.Status, result.ErrorCode);
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
