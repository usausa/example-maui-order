namespace TableOrder.TableApp.Usecase;

// 提案のルールに当たったときの内容 (足りない数と、提案する商品)
public sealed record OrderSuggestion(
    MenuResponseRule Rule,
    int Shortage,
    IReadOnlyList<MenuResponseItem> Items);

// カートに入れると超える上限の種類 (1 回の注文の明細の数、1 明細の数量、メニューのルールの上限、残りの数)
public enum CartLimitKind
{
    Lines,
    Quantity,
    Rule,
    Remaining
}

// カートに入れると超える上限と、その数 (メニューのルールの上限はルールも)
public sealed record CartLimit(
    CartLimitKind Kind,
    int Max,
    MenuResponseRule? Rule = null);

// 来店の開始と終了、メニューのルール (確認・上限・提案) の判定、注文の送信。通信と状態の更新を組み合わせる手順をここに置く
public sealed class OrderUsecase
{
    private readonly ILogger<OrderUsecase> log;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly CartState cartState;

    private readonly LanguageState languageState;

    private readonly StoreState storeState;

    private readonly ITableApi tableApi;

    public OrderUsecase(
        ILogger<OrderUsecase> log,
        MenuState menuState,
        VisitState visitState,
        CartState cartState,
        LanguageState languageState,
        StoreState storeState,
        ITableApi tableApi)
    {
        this.log = log;
        this.menuState = menuState;
        this.visitState = visitState;
        this.cartState = cartState;
        this.languageState = languageState;
        this.storeState = storeState;
        this.tableApi = tableApi;
    }

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // 来店を開く (スタッフが来店を開いた知らせを受けたとき)
    public void OpenVisit(VisitResponse visit)
    {
        cartState.Clear();
        visitState.Open(visit);
    }

    // お客様が人数を入れて来店を始める (来店の開き方が席の店)。テーブルは送らず、サーバが端末のテーブルに開く
    // 人数を入れている間にスタッフが同じテーブルを開いていたら、その来店で始める
    public async ValueTask<ApiResult<VisitResponse>> StartVisitAsync(int adults, int children)
    {
        var result = await tableApi.StartVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = adults, Children = children });
        if ((result.Status == ApiStatus.Rejected) && (result.ErrorCode == ErrorCodes.TableOccupied) &&
            ((await tableApi.GetCurrentVisitAsync()).Content is { } current))
        {
            result = ApiResult.Success(current);
        }

        if (result.Content is { } visit)
        {
            OpenVisit(visit);
        }

        return result;
    }

    // ほかのテーブルから移ってきた来店を開き、移る前の注文を読む (上限のルールに数える)
    // 注文が読めなくても開く (上限を超える注文はサーバが断る)
    public async ValueTask OpenMovedVisitAsync(VisitResponse visit)
    {
        OpenVisit(visit);
        var result = await tableApi.GetOrdersAsync(visit.Id);
        if (result.Content is { } content)
        {
            visitState.UpdateOrdered(content.Items);
        }
        else
        {
            log.WarnApiFailed(nameof(ITableApi.GetOrdersAsync), result.Status, result.ErrorCode);
        }
    }

    // 会計を終えた、または来店が終わった (閉じた、取りやめた、ほかのテーブルに移った)
    // 終える前にこのテーブルで次の来店が開いていたら (お礼の間に次のお客様を案内したなど)、続けて開く
    // 次の来店は移ってきたことも、開くまでに注文したこともあるので、注文を読む
    public ValueTask FinishVisitAsync()
    {
        var next = visitState.Next;
        visitState.Close();
        cartState.Clear();
        languageState.Reset();

        return next is not null ? OpenMovedVisitAsync(next) : ValueTask.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Availability
    //--------------------------------------------------------------------------------

    // 今の出し分け (端末の時計を店舗の現地時刻にし、来店の子どもの人数で求める)
    public MenuAvailability GetAvailability() =>
        MenuAvailability.Evaluate(menuState.Menu, storeState.LocalTime(DateTimeOffset.UtcNow), visitState.Children);

    // 商品と選んだオプションが出せる条件を満たさない理由 (満たせば None)
    public UnavailableReason FindUnavailable(MenuAvailability availability, Guid itemId, IEnumerable<Guid> optionIds) =>
        availability.ReasonOf(menuState.GetTags(itemId, optionIds));

    // カートに出せる条件を満たさない行があるか
    public bool HasUnavailableLines(MenuAvailability availability) =>
        cartState.Lines.Any(x => FindUnavailable(availability, x.ItemId, x.OptionIds) != UnavailableReason.None);

    // 終わる前の知らせを出す時間帯 (今の中で、いちばん早く終わるもの)
    public (MenuResponseDaypart Daypart, TimeSpan Remaining)? FindEndingDaypart() =>
        MenuAvailability.FindEnding(menuState.Menu, storeState.LocalTime(DateTimeOffset.UtcNow));

    //--------------------------------------------------------------------------------
    // Rule
    //--------------------------------------------------------------------------------

    // 入れる前にお客様に確かめるルール (来店で答えたものは除く)
    public IReadOnlyList<MenuResponseRule> GetRequiredConfirmations(ItemSelection selection)
    {
        var tags = menuState.GetTags(selection.ItemId, selection.OptionIds);
        return menuState.GetRules(MenuRuleKind.Confirmation)
            .Where(x => tags.Contains(x.TargetTag) && !((x.Scope == RuleScope.Visit) && visitState.IsConfirmed(x.Id)))
            .ToList();
    }

    public async ValueTask<ApiResult<VisitResponse>> ConfirmAsync(MenuResponseRule rule)
    {
        var result = await tableApi.ConfirmAsync(visitState.Id, new VisitConfirmationRequest { RuleId = rule.Id });
        if (result.Content is { } visit)
        {
            visitState.Update(visit);
        }

        return result;
    }

    // カートに入れると超える上限 (端末の中で確かめられるもの)。注文の画面と提案の追加で同じ確かめを通す
    // 直している行 (replacingLineId) は、明細の数を増やさず、数量と残りの数に数えない
    public CartLimit? FindExceededLimit(ItemSelection selection, Guid? replacingLineId = null)
    {
        var same = cartState.FindSame(selection);
        var maxLines = menuState.Config.OrderRules.MaxLinesPerOrder;
        if ((replacingLineId is null) && (same is null) && (cartState.Lines.Count >= maxLines))
        {
            return new CartLimit(CartLimitKind.Lines, maxLines);
        }

        // 同じ内容の行にまとめるときは、まとめた数で比べる (サーバは 1 明細の数量で断る)
        var maxQuantity = menuState.MaxQuantity(menuState.GetItem(selection.ItemId));
        var merged = replacingLineId is null ? same?.Quantity ?? 0 : 0;
        if (merged + selection.Quantity > maxQuantity)
        {
            return new CartLimit(CartLimitKind.Quantity, maxQuantity);
        }

        if (FindExceededRule(selection, replacingLineId) is { } rule)
        {
            return new CartLimit(CartLimitKind.Rule, rule.Max ?? 0, rule);
        }

        // 残りの数は、サーバと同じく商品とオプションごとにカートの行を合わせて数える
        foreach (var targetId in selection.OptionIds.Prepend(selection.ItemId))
        {
            if ((menuState.Remaining(targetId) is { } remaining) && (CountUsing(targetId, replacingLineId) + selection.Quantity > remaining))
            {
                return new CartLimit(CartLimitKind.Remaining, remaining);
            }
        }

        return null;
    }

    // 注文の確認で出す提案。タグの品を誰かが頼んでいて、人数より少ないときだけ出す (頼んでいない来店に毎回は出さない)
    public OrderSuggestion? GetSuggestion()
    {
        var availability = GetAvailability();
        foreach (var rule in menuState.GetRules(MenuRuleKind.Suggestion))
        {
            var count = CountTagged(rule.TargetTag, true, null);
            var guests = TagRules.Guests(rule.Basis ?? GuestBasis.Guests, visitState.Adults, visitState.Children);
            var shortage = TagRules.Shortage(count, guests);
            if ((count == 0) || (shortage == 0))
            {
                continue;
            }

            // 確認の画面の中でそのまま入れられる品 (出せる条件を満たし、必須のオプションと確認のルールがない) だけを出す
            var items = (rule.SuggestItemIds ?? [])
                .Select(menuState.GetItem)
                .Where(x => !menuState.IsSoldOut(x.Id) &&
                            availability.IsAvailable(x.Tags) &&
                            menuState.GetOptionGroups(x).All(static g => g.MinSelect == 0) &&
                            (GetRequiredConfirmations(CreateSelection(x)).Count == 0))
                .ToList();
            if (items.Count > 0)
            {
                return new OrderSuggestion(rule, shortage, items);
            }
        }

        return null;
    }

    // オプションを選ばずに 1 つ入れるときの内容
    public static ItemSelection CreateSelection(MenuResponseItem item) =>
        new(item.Id, [], 1, item.DefaultTiming);

    // 上限を超えるルール。直している行 (replacingLineId) の数は数えない
    private MenuResponseRule? FindExceededRule(ItemSelection selection, Guid? replacingLineId)
    {
        var tags = menuState.GetTags(selection.ItemId, selection.OptionIds);
        foreach (var rule in menuState.GetRules(MenuRuleKind.Limit))
        {
            if (!tags.Contains(rule.TargetTag) || (rule.Max is not { } max))
            {
                continue;
            }

            var scope = rule.Scope ?? RuleScope.Order;
            var count = CountTagged(rule.TargetTag, scope != RuleScope.Order, replacingLineId) + selection.Quantity;
            if (count > TagRules.Allowance(scope, max, visitState.Guests))
            {
                return rule;
            }
        }

        return null;
    }

    private int CountTagged(string tag, bool includeOrdered, Guid? excludingLineId)
    {
        var count = cartState.Lines
            .Where(x => (x.Id != excludingLineId) && menuState.GetTags(x.ItemId, x.OptionIds).Contains(tag))
            .Sum(static x => x.Quantity);
        if (includeOrdered)
        {
            count += visitState.OrderedLines
                .Where(x => menuState.GetTags(x.ItemId, x.OptionIds).Contains(tag))
                .Sum(static x => x.Quantity);
        }

        return count;
    }

    // カートで商品かオプションを使う数
    private int CountUsing(Guid targetId, Guid? excludingLineId) =>
        cartState.Lines
            .Where(x => (x.Id != excludingLineId) && ((x.ItemId == targetId) || x.OptionIds.Contains(targetId)))
            .Sum(static x => x.Quantity);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // カートを注文として送る。通信できなかったときは同じ Id で送り直せるように Id を残す
    public async ValueTask<ApiResult<OrderListResponseItem>> SubmitAsync()
    {
        cartState.PendingOrderId ??= Guid.CreateVersion7();
        var request = new OrderCreateRequest
        {
            Id = cartState.PendingOrderId.Value,
            MenuVersion = menuState.Menu.MenuVersion,
            Lines = cartState.Lines
                .Select(static x => new OrderCreateRequestLine
                {
                    Id = x.Id,
                    ItemId = x.ItemId,
                    OptionIds = x.OptionIds,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    Timing = x.Timing
                })
                .ToList()
        };

        var result = await tableApi.CreateOrderAsync(visitState.Id, request);
        if (result.Content is { } order)
        {
            visitState.SetOrdered(order);
            cartState.Clear();
        }
        else if (result.Status == ApiStatus.Rejected)
        {
            cartState.PendingOrderId = null;
        }

        return result;
    }

    // 送れたかわからなかった注文が届いていた (注文の通知で知った) ら、送れたものとしてカートを空にする。空にしたら true
    public bool AcceptPendingOrder(Guid orderId)
    {
        if (cartState.PendingOrderId != orderId)
        {
            return false;
        }

        cartState.Clear();
        return true;
    }
}
