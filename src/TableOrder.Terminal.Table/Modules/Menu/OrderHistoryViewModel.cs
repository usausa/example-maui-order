namespace TableOrder.Terminal.Table.Modules.Menu;

// 注文履歴。開いている間は明細の状態を読み直す (サーバの通知ができたら通知で替える)。食後の品はここからお願いする
public sealed partial class OrderHistoryViewModel : AppDialogViewModelBase
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private readonly CancellationTokenSource cancel = new();

    private readonly ILogger<OrderHistoryViewModel> log;

    private readonly IOrderApi orderApi;

    private readonly VisitState visitState;

    private readonly LanguageState languageState;

    // 操作 (お願い) で内容を反映するたびに進める。読み直しの結果は、頼んだあとに操作で反映していたら古いので使わない
    private int revision;

    public ObservableCollection<HistoryOrder> Orders { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasHeld { get; set; }

    [ObservableProperty]
    public partial string HeldText { get; set; } = string.Empty;

    public IObserveCommand ReleaseCommand { get; }

    public IObserveCommand CloseCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public OrderHistoryViewModel(
        ILogger<OrderHistoryViewModel> log,
        IPopupNavigator popupNavigator,
        IOrderApi orderApi,
        VisitState visitState,
        LanguageState languageState)
    {
        this.log = log;
        this.orderApi = orderApi;
        this.visitState = visitState;
        this.languageState = languageState;

        ReleaseCommand = MakeAsyncCommand(ReleaseAsync, () => HasHeld);
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
    // Load
    //--------------------------------------------------------------------------------

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var requested = revision;
            var result = await orderApi.GetOrdersAsync(visitState.Id, token);
            if (result.Content is { } content)
            {
                if (requested == revision)
                {
                    Apply(content);
                }
            }
            else if (result.Status != ApiStatus.Canceled)
            {
                log.WarnApiFailed(nameof(IOrderApi.GetOrdersAsync), result.Status, result.ErrorCode);
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

    private async Task ReleaseAsync()
    {
        var result = await orderApi.ReleaseAsync(visitState.Id, new OrderReleaseRequest { LineIds = [] });
        if (result.Content is { } content)
        {
            revision++;
            Apply(content);
        }
        else
        {
            log.WarnApiFailed(nameof(IOrderApi.ReleaseAsync), result.Status, result.ErrorCode);
        }
    }

    // 注文と明細が同じなら状態だけを替える (スクロールの位置を保つ)
    private void Apply(OrderListResponse content)
    {
        visitState.UpdateOrdered(content.Items);

        var current = Orders.SelectMany(static x => x.Lines).ToDictionary(static x => x.Id);
        var lines = content.Items.SelectMany(static x => x.Lines).ToList();
        if ((lines.Count == current.Count) && lines.All(x => current.ContainsKey(x.Id)))
        {
            foreach (var line in lines)
            {
                current[line.Id].Update(line.Status);
            }
        }
        else
        {
            var language = languageState.Current;
            Orders.Clear();
            foreach (var order in content.Items.OrderBy(static x => x.OrderNo))
            {
                Orders.Add(new HistoryOrder(
                    ViewHelper.Format(AppResources.HistoryOrderFormat, order.OrderNo),
                    ViewHelper.Time(order.OrderedAt),
                    order.Lines.Select(x => new HistoryLine(x, language)).ToList()));
            }
        }

        var held = lines.Where(static x => x.Status == OrderLineStatus.Held).Sum(static x => x.Quantity);
        HasHeld = held > 0;
        HeldText = ViewHelper.Format(AppResources.HistoryHeldFormat, held);
        TotalText = ViewHelper.Price(lines.Where(static x => x.Status != OrderLineStatus.Cancelled).Sum(static x => x.Amount));
        IsEmpty = content.Items.Count == 0;
        IsLoading = false;
    }
}
