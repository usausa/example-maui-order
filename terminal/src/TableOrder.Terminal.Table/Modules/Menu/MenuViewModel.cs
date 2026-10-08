namespace TableOrder.Terminal.Table.Modules.Menu;

using TableOrder.Terminal.Table.Components;

// 注文の画面。上にカテゴリのタブ、左にメニューのカード、右に注文リスト、下に履歴・呼出・会計を置く
public sealed partial class MenuViewModel : AppViewModelBase
{
    // ラストオーダーの知らせは時刻で変わるので、しばらくごとに見直す
    private static readonly TimeSpan StoreNoticeInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<MenuViewModel> log;

    private readonly IPopupNavigator popupNavigator;

    private readonly StaffLock staffLock;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly CartState cartState;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    private readonly OrderUsecase orderUsecase;

    public BrandMark Brand { get; }

    public string TableText { get; }

    // ホール端末で人数を直したら替える
    [ObservableProperty]
    public partial string GuestsText { get; set; }

    public string LanguageText { get; }

    public bool HasLanguages { get; }

    // 呼び出しの用件がない店は店員呼出を出さず、注文履歴の帯を広げる
    public bool CanCall { get; }

    public int HistorySpan => CanCall ? 1 : 2;

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

    // 店舗の知らせ (注文の一時停止、ラストオーダー)。一時停止とラストオーダーの後は注文を確定できない

    [ObservableProperty]
    public partial bool HasStoreNotice { get; set; }

    [ObservableProperty]
    public partial bool IsOrderingStopped { get; set; }

    [ObservableProperty]
    public partial string StoreNoticeGlyph { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StoreNoticeText { get; set; } = string.Empty;

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
        ImageCache imageCache,
        StaffLock staffLock,
        MenuState menuState,
        VisitState visitState,
        CartState cartState,
        LanguageState languageState,
        StoreState storeState,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.popupNavigator = popupNavigator;
        this.staffLock = staffLock;
        this.menuState = menuState;
        this.visitState = visitState;
        this.cartState = cartState;
        this.languageState = languageState;
        this.storeState = storeState;
        this.orderUsecase = orderUsecase;

        var language = languageState.Current;
        Brand = ViewHelper.Brand(menuState, imageCache, language);
        TableText = ViewHelper.Table(menuState.TableName);
        GuestsText = ViewHelper.Guests(visitState.Guests);
        LanguageText = ViewHelper.LanguageName(language);
        HasLanguages = languageState.HasChoice;
        CanCall = menuState.Config.CallReasons.Count > 0;
        Categories = menuState.GetCategories(language)
            .Select(x => new CategoryTab(x.Id, x.Name, x.Products.Select(p => new MenuCard(p, menuState.GetItem(p.Id).OptionGroupIds.Count > 0, imageCache.PathOf(p.ImageName), Brand.LogoPath)).ToList()))
            .ToList();

        SelectCategoryCommand = MakeDelegateCommand<CategoryTab>(SelectCategory);
        OpenItemCommand = MakeAsyncCommand<MenuCard>(OpenItemAsync);
        QuickAddCommand = MakeAsyncCommand<MenuCard>(QuickAddAsync);
        EditLineCommand = MakeAsyncCommand<CartLineItem>(EditLineAsync);
        IncreaseCommand = MakeAsyncCommand<CartLineItem>(IncreaseAsync);
        DecreaseCommand = MakeDelegateCommand<CartLineItem>(x => ChangeQuantity(x, x.Quantity - 1));
        SubmitCommand = MakeAsyncCommand(SubmitAsync, () => HasCart && !IsOrderingStopped);
        HistoryCommand = MakeAsyncCommand(async () => await popupNavigator.OrderHistoryAsync());
        CallCommand = MakeAsyncCommand(async () => await popupNavigator.StaffCallAsync(), () => CanCall);
        CheckoutCommand = MakeAsyncCommand(CheckoutAsync);
        LanguageCommand = MakeAsyncCommand(SelectLanguageAsync);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);

        SyncCart();
        UpdateStoreNotice();

        Disposables.Add(Observable.Interval(StoreNoticeInterval).ObserveOnCurrentContext().Subscribe(_ => UpdateStoreNotice()));
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

    // スタッフメニューを開いている間に来店が閉じていたら、来店を終えて待受に戻す
    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (visitState.Status == VisitStatus.Closed)
        {
            await Navigator.PostActionAsync(FinishVisitAsync);
        }
    }

    // お客様の画面なので、戻るでは何もしない
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;

    // レジで払い終えたなど来店が閉じたら、来店を終えて待受に戻す
    protected override Task OnVisitClosedAsync() => FinishVisitAsync();

    // ほかのテーブルに移ったら、このテーブルは待受に戻す
    protected override Task OnVisitMovedAsync() => FinishVisitAsync();

    protected override Task OnVisitUpdatedAsync()
    {
        GuestsText = ViewHelper.Guests(visitState.Guests);
        return Task.CompletedTask;
    }

    protected override Task OnStoreUpdatedAsync()
    {
        UpdateStoreNotice();
        return Task.CompletedTask;
    }

    // ホール端末・キッチン端末で売り切れにしたら、カードの表示を替える
    protected override Task OnStockUpdatedAsync()
    {
        foreach (var card in Categories.SelectMany(static x => x.Cards))
        {
            card.UpdateSoldOut(menuState.IsSoldOut(card.Id));
        }

        return Task.CompletedTask;
    }

    private async Task FinishVisitAsync()
    {
        orderUsecase.FinishVisit();
        await Navigator.ForwardAsync(ViewId.Standby);
    }

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    private void UpdateStoreNotice()
    {
        var until = storeState.UntilLastOrder(DateTimeOffset.UtcNow);
        if (storeState.OrderingPaused)
        {
            SetStoreNotice(true, ViewHelper.StoreNoticeGlyph(true), storeState.PausedMessage.Get(languageState.Current, AppResources.MenuOrderingPaused)!);
        }
        else if (until < TimeSpan.Zero)
        {
            SetStoreNotice(true, ViewHelper.StoreNoticeGlyph(false), AppResources.MenuLastOrderPassed);
        }
        else if ((menuState.Features.LastOrderNoticeMinutes > 0) && (until <= TimeSpan.FromMinutes(menuState.Features.LastOrderNoticeMinutes)) && (storeState.LastOrderTime is { } last))
        {
            SetStoreNotice(false, ViewHelper.StoreNoticeGlyph(false), ViewHelper.Format(AppResources.MenuLastOrderSoonFormat, StoreHours.Format(last)));
        }
        else
        {
            SetStoreNotice(false, string.Empty, string.Empty);
        }
    }

    private void SetStoreNotice(bool stopped, string glyph, string text)
    {
        IsOrderingStopped = stopped;
        StoreNoticeGlyph = glyph;
        StoreNoticeText = text;
        HasStoreNotice = text.Length > 0;
    }

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

    // 1 回の注文の明細の上限、メニューのルール (確認、上限)、残りの数を確かめる
    private async Task<bool> AcceptAsync(ItemSelection selection, Guid? replacingLineId)
    {
        var maxLines = menuState.Config.OrderRules.MaxLinesPerOrder;
        if ((replacingLineId is null) && cartState.AddsLine(selection) && (cartState.Lines.Count >= maxLines))
        {
            await popupNavigator.MessageAsync(AppResources.LimitTitle, ViewHelper.Format(AppResources.MaxLinesFormat, maxLines));
            return false;
        }

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
                await ShowErrorAsync(nameof(ITableApi.ConfirmAsync), result);
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

    // 言語を選び、替えたら文言を引き直すために画面を作り直す (カートは状態に残る)
    private async Task SelectLanguageAsync()
    {
        if ((await popupNavigator.LanguageAsync() is not { } language) || (language == languageState.Current))
        {
            return;
        }

        languageState.Change(language);

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
        if (await popupNavigator.VerifyStaffAsync(staffLock))
        {
            await Navigator.ForwardAsync(ViewId.Staff);
        }
    }
}
