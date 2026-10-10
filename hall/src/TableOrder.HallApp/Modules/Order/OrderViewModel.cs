namespace TableOrder.HallApp.Modules.Order;

using TableOrder.HallApp.Modules.Dialogs;

// 代わりの注文。カテゴリの帯 (カート、メニューのカテゴリ) で選んだ品を並べ、品を押すと詳細で選んでカートに入れる (出せる条件、上限、確認のルールを確かめる)
// 注文するで確かめてから送り、送れたら来店の詳細に戻る。来店は席の一覧の知らせで読み直し、終わっていたら席のタブ、会計中なら来店の詳細に戻る
// 出せる条件 (時間帯、子どもがいる) を満たさない品とカテゴリは隠し、並べ直すたびに (カテゴリを替えたとき、カートを替えたとき、読み直したとき) 求め直す
public sealed partial class OrderViewModel : AppViewModelBase
{
    private readonly ILogger<OrderViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly StoreState storeState;

    private readonly MenuState menuState;

    private readonly TableState tableState;

    private readonly IHallApi hallApi;

    private readonly ProxyOrderUsecase proxyOrderUsecase;

    private readonly List<CartLine> cart = [];

    private readonly OrderCategory cartCategory;

    // カートとすべてのカテゴリ (出せる条件を満たさないものも含む)。出す帯は Categories
    private readonly List<OrderCategory> allCategories;

    // 出している帯と品の出し分け (来店を読むまではすべて出す)
    private MenuAvailability availability = MenuAvailability.All;

    private Guid visitId;

    // 読んだ来店 (確認の記録と人数を使う)
    private VisitResponse? visit;

    // 来店の注文の明細 (上限のルールに数える)
    private IReadOnlyList<OrderListResponseLine> ordered = [];

    private OrderCategory selected;

    // 出せる品のあるカテゴリ (カートはいつも出す)。出し分けが替わったら差し替えずに入れ直す
    public ObservableCollection<OrderCategory> Categories { get; } = [];

    // カテゴリを替えたら空にしてから入れ直し、品切れの知らせでは中身を替える
    public ObservableCollection<OrderRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string TitleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOrderingPaused { get; set; }

    // 来店を読めた (読むまでは品を入れない)
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCart { get; set; }

    // 注文するの文言 (点数と合計)
    [ObservableProperty]
    public partial string SubmitText { get; set; } = string.Empty;

    public IObserveCommand BackCommand { get; }

    public IObserveCommand SelectCategoryCommand { get; }

    public IObserveCommand SelectRowCommand { get; }

    public IObserveCommand SubmitCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public OrderViewModel(
        ILogger<OrderViewModel> log,
        IPopupNavigator popupNavigator,
        StoreState storeState,
        MenuState menuState,
        TableState tableState,
        IHallApi hallApi,
        ProxyOrderUsecase proxyOrderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.storeState = storeState;
        this.menuState = menuState;
        this.tableState = tableState;
        this.hallApi = hallApi;
        this.proxyOrderUsecase = proxyOrderUsecase;

        cartCategory = new OrderCategory(true, string.Empty, [], []);
        allCategories =
        [
            cartCategory,
            .. menuState.Menu.Categories.OrderBy(static x => x.SortOrder).Select(static x => new OrderCategory(false, ViewHelper.Text(x.Name), x.ItemIds, x.Tags))
        ];
        foreach (var category in allCategories)
        {
            Categories.Add(category);
        }

        // はじめはメニューの先頭のカテゴリを出す
        selected = Categories.Count > 1 ? Categories[1] : cartCategory;
        selected.IsSelected = true;
        IsOrderingPaused = storeState.OrderingPaused;

        BackCommand = MakeAsyncCommand(BackAsync);
        SelectCategoryCommand = MakeDelegateCommand<OrderCategory>(SelectCategory);
        SelectRowCommand = MakeAsyncCommand<OrderRow>(SelectRowAsync);
        SubmitCommand = MakeAsyncCommand(SubmitAsync, () => IsLoaded && HasCart);

        UpdateCart();
        UpdateRows();
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 読むまでは、席の一覧の要約からテーブルの名前を出しておく
    public override Task OnNavigatingToAsync(INavigationContext context)
    {
        visitId = context.Parameter.GetVisitId() ?? Guid.Empty;
        if (tableState.Items.FirstOrDefault(x => x.Visit?.VisitId == visitId) is { } table)
        {
            TitleText = ViewHelper.Format(AppResources.ProxyOrderTitleFormat, table.Name);
        }

        return Task.CompletedTask;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        using (BusyState.Begin())
        {
            await LoadAsync();
        }
    }

    // 端末の戻ると通知での読み直しはコマンドの外なので、処理中にしてボタンとほかの通知と重ねない
    // (カートを捨てるかを確かめている間に、通知で下の画面が移らないように)
    protected override async Task OnNotifyBackAsync()
    {
        using (BusyState.Begin())
        {
            await BackAsync();
        }
    }

    protected override async Task OnTablesChangedAsync()
    {
        using (BusyState.Begin())
        {
            await LoadAsync();
        }
    }

    protected override Task OnStockChangedAsync()
    {
        UpdateRows();
        return Task.CompletedTask;
    }

    protected override Task OnStoreChangedAsync()
    {
        IsOrderingPaused = storeState.OrderingPaused;
        return Task.CompletedTask;
    }

    // カートに品があれば、捨ててよいか確かめてから戻る
    private async Task BackAsync()
    {
        if ((cart.Count > 0) &&
            !await popupNavigator.ConfirmAsync(AppResources.OrderDiscardTitle, AppResources.OrderDiscardMessage, AppResources.OrderDiscardOk, AppResources.CommonBack))
        {
            return;
        }

        await Navigator.ForwardAsync(ViewId.Visit, Parameters.MakeVisit(visitId));
    }

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

    // 来店と注文を読み直す。終わった来店と見つからない来店は席のタブ、会計中の来店は来店の詳細に戻る
    // 開いたときの読み込み (遷移の途中) からも移るので、遷移を終えてから移る
    // 開いたときの読み込みと通知での読み直しが重なっても、移るのは表示中のこの画面からの 1 回だけにする
    private async Task LoadAsync()
    {
        var visitResult = await hallApi.GetVisitAsync(visitId);
        if (visitResult.ErrorCode == ErrorCodes.NotFound)
        {
            await Navigator.PostForwardAsync(this, ViewId.Seats);
            return;
        }

        if (visitResult.Content is not { } current)
        {
            await FailAsync(nameof(IHallApi.GetVisitAsync), visitResult);
            return;
        }

        if (current.Status != VisitStatus.Open)
        {
            await (current.Status == VisitStatus.Paying
                ? Navigator.PostForwardAsync(this, ViewId.Visit, Parameters.MakeVisit(visitId))
                : Navigator.PostForwardAsync(this, ViewId.Seats));
            return;
        }

        var ordersResult = await hallApi.GetOrdersAsync(visitId);
        if (ordersResult.Content is not { } orders)
        {
            await FailAsync(nameof(IHallApi.GetOrdersAsync), ordersResult);
            return;
        }

        visit = current;
        ordered = orders.Items.SelectMany(static x => x.Lines).ToList();
        TitleText = ViewHelper.Format(AppResources.ProxyOrderTitleFormat, current.TableName);
        IsLoaded = true;
        UpdateRows();
    }

    private async Task FailAsync<T>(string operation, ApiResult<T> result)
    {
        log.WarnApiFailed(operation, result.Status, result.ErrorCode);
        await popupNavigator.MessageAsync(TitleText, ViewHelper.ErrorMessage(result));
    }

    //--------------------------------------------------------------------------------
    // Category
    //--------------------------------------------------------------------------------

    private void SelectCategory(OrderCategory category)
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

    // 出し分けを求め直してから、選んでいる帯の行を並べる
    private void UpdateRows()
    {
        UpdateAvailability();
        if (selected.IsCart)
        {
            Rows.Sync(cart, static (row, line) => row.Id == line.Id, CreateCartRow, (row, line) => row.UpdateAvailability(FindUnavailable(line)));
            EmptyText = AppResources.OrderCartEmpty;
        }
        else
        {
            var items = selected.ItemIds.Select(menuState.FindItem).OfType<MenuResponseItem>().Where(x => availability.IsAvailable(x.Tags)).ToList();
            Rows.Sync(items, static (row, item) => row.Id == item.Id, item => OrderRow.FromItem(item, menuState.FindStock(item.Id)), (row, item) => row.UpdateStock(menuState.FindStock(item.Id)));
            EmptyText = AppResources.StockEmpty;
        }

        IsEmpty = Rows.Count == 0;
    }

    // カートの明細 (オプションと、食後に出す品は食後にと添える)
    private OrderRow CreateCartRow(CartLine line)
    {
        var item = menuState.FindItem(line.ItemId);
        var captions = menuState.OptionNames(line.OptionIds).Select(ViewHelper.Text).ToList();
        if (line.Timing == OrderTiming.AfterMeal)
        {
            captions.Add(AppResources.StatusHeld);
        }

        var row = OrderRow.FromCart(line, item is null ? string.Empty : ViewHelper.Text(item.Name), String.Join(" / ", captions));
        row.UpdateAvailability(FindUnavailable(line));
        return row;
    }

    //--------------------------------------------------------------------------------
    // Availability
    //--------------------------------------------------------------------------------

    // 出し分けを求め直し (時刻と来店の子どもの人数)、替わっていれば帯を並べ直す。選んでいた帯がなくなったら先頭のカテゴリにする
    private void UpdateAvailability()
    {
        if (visit is null)
        {
            return;
        }

        var current = proxyOrderUsecase.GetAvailability(visit);
        if (current.IsSame(availability))
        {
            return;
        }

        availability = current;
        Categories.Clear();
        foreach (var category in allCategories.Where(IsShown))
        {
            Categories.Add(category);
        }

        if (!Categories.Contains(selected))
        {
            selected.IsSelected = false;
            selected = Categories.Count > 1 ? Categories[1] : cartCategory;
            selected.IsSelected = true;
            Rows.Clear();
        }
    }

    // カートと、出せる品のあるカテゴリを出す
    private bool IsShown(OrderCategory category) =>
        category.IsCart ||
        (availability.IsAvailable(category.Tags) && category.ItemIds.Any(id => (menuState.FindItem(id) is { } item) && availability.IsAvailable(item.Tags)));

    private UnavailableReason FindUnavailable(CartLine line) =>
        proxyOrderUsecase.FindUnavailable(availability, line.ItemId, line.OptionIds);

    private void UpdateCart()
    {
        var count = cart.Sum(static x => x.Quantity);
        var total = cart.Sum(static x => x.UnitPrice * x.Quantity);
        cartCategory.Name = ViewHelper.Format(AppResources.OrderCartFormat, count);
        HasCart = cart.Count > 0;
        SubmitText = HasCart ? ViewHelper.Format(AppResources.OrderSubmitFormat, count, ViewHelper.Price(total)) : AppResources.OrderSubmit;
    }

    //--------------------------------------------------------------------------------
    // Cart
    //--------------------------------------------------------------------------------

    private Task SelectRowAsync(OrderRow row) => row.IsCartLine ? RemoveAsync(row) : AddAsync(row);

    // 品の詳細で選び、出せる条件を満たすことと、上限を超えないことと、お客様に確かめたこと (確認のルール) を確かめてからカートに入れる
    // 出せる条件を満たさないときは、知らせてから出し分けを直す (出したままの品が時間帯の終わりを過ぎていた)
    private async Task AddAsync(OrderRow row)
    {
        if ((visit is null) || row.IsBlocked || (menuState.FindItem(row.Id) is not { } item))
        {
            return;
        }

        var rules = storeState.Config.OrderRules;
        if (cart.Count >= rules.MaxLinesPerOrder)
        {
            await popupNavigator.MessageAsync(row.Name, ViewHelper.Format(AppResources.OrderLinesLimitFormat, rules.MaxLinesPerOrder));
            return;
        }

        var maxQuantity = Math.Min(item.MaxQuantity ?? Int32.MaxValue, rules.MaxQuantityPerLine);
        if (await popupNavigator.OrderItemAsync(new OrderItemParameter(item.Id, maxQuantity, proxyOrderUsecase.GetAvailability(visit))) is not { } selection)
        {
            return;
        }

        var reason = proxyOrderUsecase.FindUnavailable(proxyOrderUsecase.GetAvailability(visit), selection.ItemId, selection.OptionIds);
        if (reason != UnavailableReason.None)
        {
            await popupNavigator.MessageAsync(AppResources.OrderUnavailableTitle, ViewHelper.UnavailableMessage(reason));
            UpdateRows();
            return;
        }

        if (proxyOrderUsecase.FindExceededLimit(visit, ordered, cart, selection) is { } limit)
        {
            await popupNavigator.MessageAsync(AppResources.OrderLimitTitle, limit.Message is { } message ? ViewHelper.Text(message) : AppResources.OrderLimitMessage);
            return;
        }

        foreach (var rule in proxyOrderUsecase.GetRequiredConfirmations(visit, selection))
        {
            var message = rule.Message is { } text ? ViewHelper.Text(text) : AppResources.OrderConfirmRuleFallback;
            if (!await popupNavigator.ConfirmAsync(AppResources.OrderConfirmRuleTitle, message, AppResources.OrderConfirmRuleOk, AppResources.CommonBack))
            {
                return;
            }

            var result = await proxyOrderUsecase.ConfirmAsync(visit, rule);
            if (result.Content is not { } confirmed)
            {
                await FailAsync(nameof(IHallApi.ConfirmAsync), result);
                return;
            }

            visit = confirmed;
        }

        cart.Add(new CartLine(Guid.CreateVersion7(), selection.ItemId, selection.OptionIds, selection.Quantity, selection.Timing, menuState.UnitPrice(selection.ItemId, selection.OptionIds)));
        UpdateCart();
        UpdateRows();
    }

    private async Task RemoveAsync(OrderRow row)
    {
        if (!await popupNavigator.ConfirmAsync(AppResources.OrderRemoveTitle, ViewHelper.Format(AppResources.OrderRemoveMessageFormat, row.Name), AppResources.OrderRemoveOk, AppResources.CommonBack))
        {
            return;
        }

        cart.RemoveAll(x => x.Id == row.Id);
        UpdateCart();
        UpdateRows();
    }

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 確かめてから送る。送れたら来店の詳細に戻り、断られたら理由を知らせる (通信できなかったときはカートを残して送り直せる)
    // 送り直しでなければ、送る前に出せる条件を確かめる (満たさない明細はカートの帯を開いて印を出し、外してから送ってもらう)
    private async Task SubmitAsync()
    {
        if ((visit is null) || (cart.Count == 0))
        {
            return;
        }

        var current = proxyOrderUsecase.GetAvailability(visit);
        if (!proxyOrderUsecase.IsResend(visit, cart) &&
            cart.Any(x => proxyOrderUsecase.FindUnavailable(current, x.ItemId, x.OptionIds) != UnavailableReason.None))
        {
            await popupNavigator.MessageAsync(AppResources.OrderSubmit, AppResources.OrderCartUnavailable);
            SelectCategory(cartCategory);
            UpdateRows();
            return;
        }

        var count = cart.Sum(static x => x.Quantity);
        var total = cart.Sum(static x => x.UnitPrice * x.Quantity);
        var message = ViewHelper.Format(AppResources.OrderSubmitMessageFormat, visit.TableName, count, ViewHelper.Price(total));
        if (!await popupNavigator.ConfirmAsync(AppResources.OrderSubmit, message, AppResources.OrderSubmit, AppResources.CommonBack))
        {
            return;
        }

        var result = await proxyOrderUsecase.SubmitAsync(visit, cart);
        if (result.IsSuccess)
        {
            cart.Clear();
            await Navigator.ForwardAsync(ViewId.Visit, Parameters.MakeVisit(visitId));
            return;
        }

        await FailAsync(nameof(IHallApi.CreateOrderAsync), result);
    }
}
