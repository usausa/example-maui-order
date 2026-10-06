namespace TableOrder.Terminal.Table.Modules.Menu;

// 注文の画面。上にカテゴリのタブ、左にメニューのカード、右に注文リスト、下に履歴・呼出・会計を置く
public sealed partial class MenuViewModel : AppViewModelBase
{
    private readonly ILogger<MenuViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly Settings settings;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly CartState cartState;

    private readonly LanguageState languageState;

    private readonly OrderUsecase orderUsecase;

    public string TableText { get; }

    public string GuestsText { get; }

    public string LanguageText { get; }

    public IReadOnlyList<CategoryTab> Categories { get; }

    [ObservableProperty]
    public partial IReadOnlyList<MenuCard> Cards { get; set; } = [];

    public ObservableCollection<CartLineItem> CartLines { get; } = [];

    [ObservableProperty]
    public partial string CartCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CartTotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCart { get; set; }

    public IObserveCommand SelectCategoryCommand { get; }

    public IObserveCommand OpenItemCommand { get; }

    public IObserveCommand QuickAddCommand { get; }

    public IObserveCommand EditLineCommand { get; }

    public IObserveCommand IncreaseCommand { get; }

    public IObserveCommand DecreaseCommand { get; }

    public IObserveCommand SubmitCommand { get; }

    public IObserveCommand HistoryCommand { get; }

    public IObserveCommand CallCommand { get; }

    public IObserveCommand CheckoutCommand { get; }

    public IObserveCommand LanguageCommand { get; }

    public IObserveCommand StaffCommand { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public MenuViewModel(
        ILogger<MenuViewModel> log,
        IPopupNavigator popupNavigator,
        Settings settings,
        MenuState menuState,
        VisitState visitState,
        CartState cartState,
        LanguageState languageState,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.settings = settings;
        this.menuState = menuState;
        this.visitState = visitState;
        this.cartState = cartState;
        this.languageState = languageState;
        this.orderUsecase = orderUsecase;

        var language = languageState.Current;
        TableText = ViewHelper.Table(settings.TableNo);
        GuestsText = ViewHelper.Guests(visitState.Guests);
        LanguageText = ViewHelper.SwitchName(language);
        Categories = menuState.GetCategories(language)
            .Select(x => new CategoryTab(x.Id, x.Name, x.Products.Select(p => new MenuCard(p, menuState.GetItem(p.Id).OptionGroupIds.Count > 0)).ToList()))
            .ToList();

        SelectCategoryCommand = MakeDelegateCommand<CategoryTab>(SelectCategory);
        OpenItemCommand = MakeAsyncCommand<MenuCard>(OpenItemAsync);
        QuickAddCommand = MakeAsyncCommand<MenuCard>(QuickAddAsync);
        EditLineCommand = MakeAsyncCommand<CartLineItem>(EditLineAsync);
        IncreaseCommand = MakeAsyncCommand<CartLineItem>(IncreaseAsync);
        DecreaseCommand = MakeDelegateCommand<CartLineItem>(x => ChangeQuantity(x, x.Quantity - 1));
        SubmitCommand = MakeAsyncCommand(SubmitAsync, () => HasCart);
        HistoryCommand = MakeAsyncCommand(async () => await popupNavigator.OrderHistoryAsync());
        CallCommand = MakeAsyncCommand(async () => await popupNavigator.StaffCallAsync());
        CheckoutCommand = MakeAsyncCommand(CheckoutAsync);
        LanguageCommand = MakeAsyncCommand(SwitchLanguageAsync);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);

        SyncCart();
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 言語を切り替えて作り直したときは、選んでいたカテゴリを開く
    public override Task OnNavigatingToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetCategoryId();
        var tab = Categories.FirstOrDefault(x => x.Id == id) ?? (Categories.Count > 0 ? Categories[0] : null);
        if (tab is not null)
        {
            SelectCategory(tab);
        }

        return Task.CompletedTask;
    }

    // お客様の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Category
    //--------------------------------------------------------------------------------

    private void SelectCategory(CategoryTab tab)
    {
        foreach (var category in Categories)
        {
            category.IsSelected = category == tab;
        }

        Cards = tab.Cards;
    }

    //--------------------------------------------------------------------------------
    // Item
    //--------------------------------------------------------------------------------

    private async Task OpenItemAsync(MenuCard card)
    {
        if (card.IsSoldOut)
        {
            return;
        }

        if (await popupNavigator.ItemDetailAsync(card.Id) is { } selection)
        {
            await AddAsync(selection);
        }
    }

    // オプションのない商品は 1 つすぐ入れる (出す時機は商品の既定)
    private Task QuickAddAsync(MenuCard card) =>
        card.CanQuickAdd ? AddAsync(OrderUsecase.CreateSelection(menuState.GetItem(card.Id))) : Task.CompletedTask;

    private async Task AddAsync(ItemSelection selection)
    {
        if (!await AcceptAsync(selection, null))
        {
            return;
        }

        cartState.Add(selection, menuState.UnitPrice(selection.ItemId, selection.OptionIds));
        SyncCart();
    }

    // メニューのルール (確認、上限) と残りの数を確かめる
    private async Task<bool> AcceptAsync(ItemSelection selection, Guid? replacingLineId)
    {
        var language = languageState.Current;
        foreach (var rule in orderUsecase.GetRequiredConfirmations(selection))
        {
            var message = rule.Message.Get(language, AppResources.RuleConfirmMessage)!;
            if (!await popupNavigator.ConfirmAsync(AppResources.RuleConfirmTitle, message, AppResources.CommonYes, AppResources.CommonNo))
            {
                return false;
            }

            var result = await orderUsecase.ConfirmAsync(rule);
            if (!result.IsSuccess)
            {
                await ShowErrorAsync(nameof(IOrderApi.ConfirmAsync), result);
                return false;
            }
        }

        if (orderUsecase.FindExceededLimit(selection, replacingLineId) is { } limit)
        {
            var name = menuState.TagName(limit.TargetTag, language);
            await popupNavigator.MessageAsync(AppResources.LimitTitle, ViewHelper.Format(AppResources.LimitMessageFormat, name));
            return false;
        }

        if (menuState.Remaining(selection.ItemId) is { } remaining)
        {
            var count = cartState.Lines.Where(x => (x.ItemId == selection.ItemId) && (x.Id != replacingLineId)).Sum(static x => x.Quantity);
            if (count + selection.Quantity > remaining)
            {
                await popupNavigator.MessageAsync(AppResources.LimitTitle, ViewHelper.Format(AppResources.StockRemainingFormat, remaining));
                return false;
            }
        }

        return true;
    }

    //--------------------------------------------------------------------------------
    // Cart
    //--------------------------------------------------------------------------------

    private async Task EditLineAsync(CartLineItem item)
    {
        if (await popupNavigator.ItemDetailAsync(item.Line.ItemId, item.Line) is not { } selection)
        {
            return;
        }

        if (!await AcceptAsync(selection, item.Id))
        {
            return;
        }

        cartState.Replace(item.Id, selection, menuState.UnitPrice(selection.ItemId, selection.OptionIds));
        SyncCart();
    }

    private async Task IncreaseAsync(CartLineItem item)
    {
        var line = item.Line;
        if (line.Quantity >= menuState.MaxQuantity(menuState.GetItem(line.ItemId)))
        {
            return;
        }

        // 1 つ足した数でルールを確かめる (この行の今の数は数に入っている)
        if (!await AcceptAsync(new ItemSelection(line.ItemId, line.OptionIds, line.Quantity + 1, line.Timing), line.Id))
        {
            return;
        }

        ChangeQuantity(item, line.Quantity + 1);
    }

    private void ChangeQuantity(CartLineItem item, int quantity)
    {
        cartState.SetQuantity(item.Id, quantity);
        SyncCart();
    }

    // 行を作り直さずに数量と金額を替える (スクロールの位置を保つ)。内容を直した行だけ作り直す
    private void SyncCart()
    {
        for (var i = CartLines.Count - 1; i >= 0; i--)
        {
            if (cartState.Find(CartLines[i].Id) is null)
            {
                CartLines.RemoveAt(i);
            }
        }

        for (var i = 0; i < cartState.Lines.Count; i++)
        {
            var line = cartState.Lines[i];
            var index = IndexOf(line.Id);
            if ((index >= 0) && CartLines[index].Line.IsSameSelection(line.ItemId, line.OptionIds, line.Timing))
            {
                CartLines[index].Update(line);
                if (index != i)
                {
                    CartLines.Move(index, i);
                }
            }
            else
            {
                if (index >= 0)
                {
                    CartLines.RemoveAt(index);
                }

                CartLines.Insert(i, CreateLineItem(line));
            }
        }

        HasCart = cartState.Lines.Count > 0;
        CartCountText = cartState.Count.ToString(CultureInfo.InvariantCulture);
        CartTotalText = ViewHelper.Price(cartState.Total);
    }

    private int IndexOf(Guid id)
    {
        for (var i = 0; i < CartLines.Count; i++)
        {
            if (CartLines[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private CartLineItem CreateLineItem(CartLine line)
    {
        var language = languageState.Current;
        return new CartLineItem(line, menuState.GetItem(line.ItemId).Name.Get(language), menuState.OptionText(line.OptionIds, language));
    }

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

    private async Task SubmitAsync()
    {
        await popupNavigator.OrderConfirmAsync();

        // 提案で足した品と、送れたときに空になったカートを映す
        SyncCart();
    }

    private async Task CheckoutAsync()
    {
        if (visitState.OrderedLines.Count == 0)
        {
            await popupNavigator.MessageAsync(AppResources.CheckoutTitle, AppResources.CheckoutNoOrder);
            return;
        }

        if (HasCart && !await popupNavigator.ConfirmAsync(AppResources.CheckoutTitle, AppResources.CheckoutUnordered, AppResources.CheckoutProceed, AppResources.CommonBack))
        {
            return;
        }

        await Navigator.ForwardAsync(ViewId.Checkout);
    }

    // 文言を引き直すために画面を作り直す (カートは状態に残る)
    private async Task SwitchLanguageAsync()
    {
        languageState.Change(languageState.Current == Language.Japanese ? Language.English : Language.Japanese);

        var selected = Categories.FirstOrDefault(static x => x.IsSelected);
        await Navigator.ForwardAsync(ViewId.Menu, selected is null ? null : Parameters.MakeCategoryId(selected.Id));
    }

    private async Task ShowErrorAsync<T>(string operation, ApiResult<T> result)
    {
        log.WarnApiFailed(operation, result.Status, result.ErrorCode);
        await popupNavigator.MessageAsync(AppResources.ErrorTitle, ViewHelper.ErrorMessage(result));
    }

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // ブランドの印の長押しで、PIN を確かめてスタッフメニューに入る
    private async Task OpenStaffAsync()
    {
        if (await popupNavigator.VerifyStaffAsync(settings))
        {
            await Navigator.ForwardAsync(ViewId.Staff);
        }
    }
}
