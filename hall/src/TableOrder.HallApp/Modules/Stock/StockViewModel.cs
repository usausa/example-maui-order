namespace TableOrder.HallApp.Modules.Stock;

using TableOrder.HallApp.Modules.Dialogs;

// 品切れのタブ。カテゴリの帯 (品切れ・残り、メニューのカテゴリ、オプション) で選んだ品を並べ、品を押すと状態を選んで送る
// 下の帯に、注文の一時停止と再開 (どちらかだけ)、すべて戻すを置く。品は品切れの通知 (StockChanged) で出し直す
public sealed partial class StockViewModel : TabViewModelBase
{
    // 残りの数の桁 (Length.MaxStockRemaining まで)
    private const int RemainingDigits = 4;

    private readonly IPopupNavigator popupNavigator;

    private readonly MenuState menuState;

    private readonly HallUsecase hallUsecase;

    // 商品 (メニューの並び) とオプション (グループの並び)
    private readonly IReadOnlyList<StockTarget> items;

    private readonly Dictionary<Guid, StockTarget> itemById;

    private readonly IReadOnlyList<StockTarget> options;

    private StockCategory selected;

    public IReadOnlyList<StockCategory> Categories { get; }

    // カテゴリを替えたら空にしてから入れ直し (一覧を先頭から見せる)、品切れの通知では中身を替える
    public ObservableCollection<StockRow> Rows { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; } = string.Empty;

    // 品切れと残りの数のある品がある (すべて戻すを押せる)
    [ObservableProperty]
    public partial bool HasStocks { get; set; }

    public IObserveCommand SelectCategoryCommand { get; }

    public IObserveCommand EditCommand { get; }

    public IObserveCommand ResetCommand { get; }

    public IObserveCommand PauseCommand { get; }

    public IObserveCommand ResumeCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public StockViewModel(
        IPopupNavigator popupNavigator,
        StoreState storeState,
        MenuState menuState,
        CallState callState,
        ServingState servingState,
        HallUsecase hallUsecase)
        : base(ViewId.Stock, storeState, callState, servingState)
    {
        this.popupNavigator = popupNavigator;
        this.menuState = menuState;
        this.hallUsecase = hallUsecase;

        var menu = menuState.Menu;
        items = menu.Items.Select(static x => new StockTarget(x.Id, StockTargetKind.Item, ViewHelper.Text(x.Name), string.Empty)).ToList();
        itemById = items.ToDictionary(static x => x.Id);
        options = menu.OptionGroups
            .SelectMany(static g => g.Options.Select(o => new StockTarget(o.Id, StockTargetKind.Option, ViewHelper.Text(o.Name), ViewHelper.Text(g.Name))))
            .ToList();

        Categories =
        [
            new StockCategory(StockCategoryKind.Limited, AppResources.StockTabLimited, []),
            .. menu.Categories.OrderBy(static x => x.SortOrder).Select(static x => new StockCategory(StockCategoryKind.Category, ViewHelper.Text(x.Name), x.ItemIds)),
            new StockCategory(StockCategoryKind.Options, AppResources.StockTabOptions, [])
        ];
        selected = Categories[0];
        selected.IsSelected = true;

        SelectCategoryCommand = MakeDelegateCommand<StockCategory>(SelectCategory);
        EditCommand = MakeAsyncCommand<StockRow>(EditAsync);
        ResetCommand = MakeAsyncCommand(ResetAsync, () => HasStocks);
        PauseCommand = MakeAsyncCommand(PauseAsync);
        ResumeCommand = MakeAsyncCommand(ResumeAsync);

        UpdateRows();
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    protected override Task OnStockChangedAsync()
    {
        UpdateRows();
        return Task.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Category
    //--------------------------------------------------------------------------------

    private void SelectCategory(StockCategory category)
    {
        if (category == selected)
        {
            return;
        }

        selected.IsSelected = false;
        selected = category;
        selected.IsSelected = true;
        Rows.Clear();
        UpdateRows();
    }

    private void UpdateRows()
    {
        var targets = selected.Kind switch
        {
            StockCategoryKind.Limited => items.Concat(options).Where(x => menuState.FindStock(x.Id) is not null).ToList(),
            StockCategoryKind.Category => selected.ItemIds.Select(x => itemById.GetValueOrDefault(x)).OfType<StockTarget>().ToList(),
            _ => options
        };
        Rows.Sync(targets, static (row, target) => row.TargetId == target.Id, target => new StockRow(target, menuState.FindStock(target.Id)), (row, target) => row.Update(menuState.FindStock(target.Id)));
        IsEmpty = Rows.Count == 0;
        EmptyText = selected.Kind == StockCategoryKind.Limited ? AppResources.StockEmptyLimited : AppResources.StockEmpty;
        HasStocks = menuState.Stocks.Count > 0;
    }

    //--------------------------------------------------------------------------------
    // Stock
    //--------------------------------------------------------------------------------

    // 状態を選ぶ。残りの数は電卓で入れ (0 はサーバが品切れにする)、今と同じ状態 (残りの数のほか) は送らない
    private async Task EditAsync(StockRow row)
    {
        var current = ViewHelper.Format(AppResources.StockCurrentFormat, ViewHelper.Name(row.Status, row.Remaining));
        if (await popupNavigator.StockEditAsync(new StockEditParameter(row.Name, row.Caption, row.Status, current)) is not { } status)
        {
            return;
        }

        int? remaining = null;
        if (status == StockStatus.Limited)
        {
            var text = await popupNavigator.InputNumberAsync(AppResources.StockRemainingTitle, row.Remaining?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, RemainingDigits);
            if (!Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return;
            }

            remaining = value;
        }
        else if (status == row.Status)
        {
            return;
        }

        await AfterStockAsync(row.Name, await hallUsecase.UpdateStockAsync(row.TargetId, row.Kind, status, remaining));
    }

    private async Task ResetAsync()
    {
        if (await popupNavigator.ConfirmAsync(AppResources.StockReset, AppResources.StockResetMessage, AppResources.StockResetOk, AppResources.CommonBack))
        {
            await AfterStockAsync(AppResources.StockReset, await hallUsecase.ResetStockAsync());
        }
    }

    // 読み直した品切れを出し、断られたら知らせる
    private async Task AfterStockAsync(string title, ApiResult<NoContent> result)
    {
        UpdateRows();
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        }
    }

    //--------------------------------------------------------------------------------
    // Ordering
    //--------------------------------------------------------------------------------

    // 注文を止めるとお客様が注文できなくなるので、確認してから送る
    private async Task PauseAsync()
    {
        if (await popupNavigator.ConfirmAsync(AppResources.OrderingPause, AppResources.OrderingPauseMessage, AppResources.OrderingPauseOk, AppResources.CommonBack))
        {
            await AfterOrderingAsync(AppResources.OrderingPause, await hallUsecase.SetOrderingAsync(true));
        }
    }

    private async Task ResumeAsync() =>
        await AfterOrderingAsync(AppResources.OrderingResume, await hallUsecase.SetOrderingAsync(false));

    // 読み直した店舗の状態を出し (ヘッダの知らせの帯も)、断られたら知らせる
    private async Task AfterOrderingAsync(string title, ApiResult<NoContent> result)
    {
        await OnStoreChangedAsync();
        if (!result.IsSuccess)
        {
            await popupNavigator.MessageAsync(title, ViewHelper.ErrorMessage(result));
        }
    }
}
