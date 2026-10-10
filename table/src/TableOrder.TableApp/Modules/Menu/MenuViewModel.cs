namespace TableOrder.TableApp.Modules.Menu;

using TableOrder.Terminal.Components;

// 注文の画面。上にカテゴリのタブ、左にメニューのカード、右に注文リスト、下に履歴・呼出・会計を置く
public sealed partial class MenuViewModel : AppViewModelBase
{
    // ラストオーダーの知らせは時刻で変わるので、しばらくごとに見直す
    private static readonly TimeSpan OrderNoticeInterval = TimeSpan.FromSeconds(30);

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

    // 注文の知らせ (会計中、注文の一時停止、ラストオーダー)。会計中・一時停止・ラストオーダーの後は注文を確定できない

    [ObservableProperty]
    public partial bool HasOrderNotice { get; set; }

    [ObservableProperty]
    public partial bool IsOrderingStopped { get; set; }

    [ObservableProperty]
    public partial string OrderNoticeGlyph { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OrderNoticeText { get; set; } = string.Empty;

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
        LanguageText = language.NativeName();
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
        DecreaseCommand = MakeAsyncCommand<CartLineItem>(DecreaseAsync);
        SubmitCommand = MakeAsyncCommand(SubmitAsync, () => HasCart && !IsOrderingStopped);
        HistoryCommand = MakeAsyncCommand(async () => await popupNavigator.OrderHistoryAsync());
        CallCommand = MakeAsyncCommand(async () => await popupNavigator.StaffCallAsync(), () => CanCall);
        CheckoutCommand = MakeAsyncCommand(CheckoutAsync);
        LanguageCommand = MakeAsyncCommand(SelectLanguageAsync);
        StaffCommand = MakeAsyncCommand(OpenStaffAsync);

        SyncCart();
        UpdateOrderNotice();

        Disposables.Add(Observable.Interval(OrderNoticeInterval).ObserveOnCurrentContext().Subscribe(_ => UpdateOrderNotice()));
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

    // スタッフメニューを開いている間に来店が終わっていたら (閉じた、取りやめた、ほかのテーブルに移った)、来店を終える
    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (visitState.IsFinished)
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

    // ホール端末で人数を直したときと、会計を始めた・やめたときに出し直す
    protected override Task OnVisitUpdatedAsync()
    {
        GuestsText = ViewHelper.Guests(visitState.Guests);
        UpdateOrderNotice();
        return Task.CompletedTask;
    }

    protected override Task OnStoreUpdatedAsync()
    {
        UpdateOrderNotice();
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

    // 送れたかわからなかった注文が届いていたら、空にしたカートを映して受け付けたことを知らせる
    // 知らせを開いている間は、画面のボタンと重ならないように Busy にする
    protected override async Task OnOrderAcceptedAsync()
    {
        SyncCart();
        using (BusyState.Begin())
        {
            await popupNavigator.MessageAsync(AppResources.ConfirmDoneTitle, AppResources.ConfirmDoneMessage);
        }
    }

    // 来店を終えて待受に戻す。終える前に次の来店が開いていたら、次の来店の注文の画面にする
    // 次の来店の注文を読む間も画面の操作と重ならないように、Busy にする
    private async Task FinishVisitAsync()
    {
        using (BusyState.Begin())
        {
            await orderUsecase.FinishVisitAsync();
            await Navigator.ForwardAsync(visitState.IsOpen ? ViewId.Menu : ViewId.Standby);
        }
    }

    //--------------------------------------------------------------------------------
    // Notice
    //--------------------------------------------------------------------------------

    // ほかの端末 (ホール端末) で会計を始めたら、会計の明細が変わらないように確定を止める (お会計の画面には進める)
    private void UpdateOrderNotice()
    {
        var until = storeState.UntilLastOrder(DateTimeOffset.UtcNow);
        if (visitState.Status == VisitStatus.Paying)
        {
            SetOrderNotice(true, ViewHelper.CheckoutNoticeGlyph, AppResources.MenuCheckoutInProgress);
        }
        else if (storeState.OrderingPaused)
        {
            SetOrderNotice(true, ViewHelper.OrderNoticeGlyph(true), storeState.PausedMessage.Get(languageState.Current, AppResources.MenuOrderingPaused)!);
        }
        else if (until < TimeSpan.Zero)
        {
            SetOrderNotice(true, ViewHelper.OrderNoticeGlyph(false), AppResources.MenuLastOrderPassed);
        }
        else if ((menuState.Features.LastOrderNoticeMinutes > 0) && (until <= TimeSpan.FromMinutes(menuState.Features.LastOrderNoticeMinutes)) && (storeState.LastOrderTime is { } last))
        {
            SetOrderNotice(false, ViewHelper.OrderNoticeGlyph(false), ViewHelper.Format(AppResources.MenuLastOrderSoonFormat, StoreHours.Format(last)));
        }
        else
        {
            SetOrderNotice(false, string.Empty, string.Empty);
        }
    }

    private void SetOrderNotice(bool stopped, string glyph, string text)
    {
        IsOrderingStopped = stopped;
        OrderNoticeGlyph = glyph;
        OrderNoticeText = text;
        HasOrderNotice = text.Length > 0;
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
        if (card.IsSoldOut || !await CanEditCartAsync())
        {
            return;
        }

        if (await popupNavigator.ItemDetailAsync(card.Id) is { } selection)
        {
            await AddAsync(selection);
        }
    }

    // オプションのない商品は 1 つすぐ入れる (出す時機は商品の既定)
    private async Task QuickAddAsync(MenuCard card)
    {
        if (card.CanQuickAdd && await CanEditCartAsync())
        {
            await AddAsync(OrderUsecase.CreateSelection(menuState.GetItem(card.Id)));
        }
    }

    private async Task AddAsync(ItemSelection selection)
    {
        if (!await AcceptAsync(selection, null))
        {
            return;
        }

        cartState.Add(selection, menuState.UnitPrice(selection.ItemId, selection.OptionIds));
        SyncCart();
    }

    // 上限 (1 回の注文の明細の数、1 明細の数量、メニューのルールの上限、残りの数) と確認のルールを確かめる
    // 端末の中で確かめられる上限を先に見て、入れられないものにお客様の確認を求めない
    private async Task<bool> AcceptAsync(ItemSelection selection, Guid? replacingLineId)
    {
        var language = languageState.Current;
        if (orderUsecase.FindExceededLimit(selection, replacingLineId) is { } limit)
        {
            await popupNavigator.MessageAsync(AppResources.LimitTitle, ViewHelper.LimitMessage(limit, menuState, language));
            return false;
        }

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

        return true;
    }

    //--------------------------------------------------------------------------------
    // Cart
    //--------------------------------------------------------------------------------

    private async Task EditLineAsync(CartLineItem item)
    {
        if (!await CanEditCartAsync() || (await popupNavigator.ItemDetailAsync(item.Line.ItemId, item.Line) is not { } selection))
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

    // 1 つ足した数で上限とルールを確かめる (この行の今の数は数に入っている)
    private async Task IncreaseAsync(CartLineItem item)
    {
        var line = item.Line;
        if (await CanEditCartAsync() && await AcceptAsync(new ItemSelection(line.ItemId, line.OptionIds, line.Quantity + 1, line.Timing), line.Id))
        {
            ChangeQuantity(item, line.Quantity + 1);
        }
    }

    private async Task DecreaseAsync(CartLineItem item)
    {
        if (await CanEditCartAsync())
        {
            ChangeQuantity(item, item.Quantity - 1);
        }
    }

    // 送れたかわからない注文があるうちは、カートを直させずに同じ内容で送り直してもらう
    // (直すと、届いていた注文と二重になるか、同じ Id の違う内容として断られる)
    private async Task<bool> CanEditCartAsync()
    {
        if (!cartState.HasPendingOrder)
        {
            return true;
        }

        await popupNavigator.MessageAsync(AppResources.ConfirmTitle, AppResources.CartPendingMessage);
        return false;
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
