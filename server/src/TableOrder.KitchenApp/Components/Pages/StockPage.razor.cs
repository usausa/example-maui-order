namespace TableOrder.KitchenApp.Components.Pages;

// 品切れ。カテゴリの帯 (品切れ・残り、メニューのカテゴリ、オプション) で選んだ品を並べ、品を押すと状態を選んで送る
// すべて戻すと注文の一時停止はホール端末だけにする。品は品切れの通知で出し直す
public sealed partial class StockPage : IDisposable
{
    private IReadOnlyList<StockCategory> categories = [];

    // 商品 (メニューの並び) とオプション (組の並び)
    private IReadOnlyList<StockTarget> items = [];

    private Dictionary<Guid, StockTarget> itemById = [];

    private IReadOnlyList<StockTarget> options = [];

    private StockCategory selected = default!;

    // 状態を選んでいる品
    private StockTarget? editing;

    private bool isBusy;

    private string? message;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required ILogger<StockPage> Log { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required StoreState StoreState { get; set; }

    [Inject]
    public required StockState StockState { get; set; }

    [Inject]
    public required KitchenUsecase KitchenUsecase { get; set; }

    [Inject]
    public required KitchenEventReceiver Receiver { get; set; }

    private IReadOnlyList<StockTarget> Rows =>
        selected.Kind switch
        {
            StockCategoryKind.Limited => items.Concat(options).Where(x => StockState.Find(x.Id) is not null).ToList(),
            StockCategoryKind.Category => selected.ItemIds.Select(x => itemById.GetValueOrDefault(x)).OfType<StockTarget>().ToList(),
            _ => options
        };

    private string EmptyText => selected.Kind == StockCategoryKind.Limited ? AppResources.StockEmptyLimited : AppResources.StockEmpty;

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override void OnInitialized()
    {
        var menu = StoreState.Menu;
        items = menu.Items.Select(static x => new StockTarget(x.Id, StockTargetKind.Item, ViewHelper.Text(x.Name), string.Empty)).ToList();
        itemById = items.ToDictionary(static x => x.Id);
        options = menu.OptionGroups
            .SelectMany(static g => g.Options.Select(o => new StockTarget(o.Id, StockTargetKind.Option, ViewHelper.Text(o.Name), ViewHelper.Text(g.Name))))
            .ToList();
        categories =
        [
            new StockCategory(StockCategoryKind.Limited, AppResources.StockTabLimited, []),
            .. menu.Categories.OrderBy(static x => x.SortOrder).Select(static x => new StockCategory(StockCategoryKind.Category, ViewHelper.Text(x.Name), x.ItemIds)),
            new StockCategory(StockCategoryKind.Options, AppResources.StockTabOptions, [])
        ];
        selected = categories[0];

        Receiver.Changed += OnChanged;
    }

    public void Dispose() => Receiver.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);

    //--------------------------------------------------------------------------------
    // Category
    //--------------------------------------------------------------------------------

    private void Select(StockCategory category) => selected = category;

    private string StatusText(StockTarget target) =>
        StockState.Find(target.Id) is { } stock ? ViewHelper.StockName(stock.Status, stock.Remaining) : string.Empty;

    // 残りの数は注意の色にし、品切れと見分ける
    private string? StatusClass(StockTarget target) =>
        StockState.Find(target.Id)?.Status == StockStatus.Limited ? "row-status-limited" : null;

    //--------------------------------------------------------------------------------
    // Stock
    //--------------------------------------------------------------------------------

    private void Edit(StockTarget target) => editing = target;

    private void CancelEdit() => editing = null;

    // 今と同じ状態 (残りの数のほか) は送らない
    private async Task DecideAsync(StockDecision decision)
    {
        if (editing is not { } target)
        {
            return;
        }

        editing = null;
        var current = StockState.Find(target.Id)?.Status ?? StockStatus.Available;
        if ((decision.Status != StockStatus.Limited) && (decision.Status == current))
        {
            return;
        }

        isBusy = true;
        try
        {
            var result = await KitchenUsecase.UpdateStockAsync(target.Id, target.Kind, decision.Status, decision.Remaining);
            if (!result.IsSuccess)
            {
                Log.WarnApiFailed(nameof(KitchenUsecase.UpdateStockAsync), result.Status, result.ErrorCode);
                message = ViewHelper.ErrorMessage(result);
            }
        }
        finally
        {
            isBusy = false;
        }
    }

    private void CloseMessage() => message = null;

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void Close() => Navigation.NavigateTo("tickets", replace: true);
}
