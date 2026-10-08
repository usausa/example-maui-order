namespace TableOrder.Terminal.Table.Usecase;

// 提案のルールに当たったときの内容 (足りない数と、提案する商品)
public sealed record OrderSuggestion(
    MenuResponseRule Rule,
    int Shortage,
    IReadOnlyList<MenuResponseItem> Items);

// 来店の開始と終了、メニューのルール (確認・上限・提案) の判定、注文の送信。通信と状態の更新を組み合わせる手順をここに置く
public sealed class OrderUsecase
{
    private readonly ITableApi tableApi;

    private readonly MenuState menuState;

    private readonly VisitState visitState;

    private readonly CartState cartState;

    private readonly LanguageState languageState;

    public OrderUsecase(
        ITableApi tableApi,
        MenuState menuState,
        VisitState visitState,
        CartState cartState,
        LanguageState languageState)
    {
        this.tableApi = tableApi;
        this.menuState = menuState;
        this.visitState = visitState;
        this.cartState = cartState;
        this.languageState = languageState;
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

    // ほかのテーブルから移ってきた来店を開き、移る前の注文を読む (上限のルールに数える)
    public async ValueTask<ApiResult<OrderListResponse>> OpenMovedVisitAsync(VisitResponse visit)
    {
        OpenVisit(visit);
        var result = await tableApi.GetOrdersAsync(visit.Id);
        if (result.Content is { } content)
        {
            visitState.UpdateOrdered(content.Items);
        }

        return result;
    }

    // 会計を終えた、または来店が閉じられた
    public void FinishVisit()
    {
        visitState.Close();
        cartState.Clear();
        languageState.Reset();
    }

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

    // 上限を超えるルール。直している行 (replacingLineId) の数は数えない
    public MenuResponseRule? FindExceededLimit(ItemSelection selection, Guid? replacingLineId = null)
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

    // 注文の確認で出す提案。タグの品を誰かが頼んでいて、人数より少ないときだけ出す (頼んでいない来店に毎回は出さない)
    public OrderSuggestion? GetSuggestion()
    {
        foreach (var rule in menuState.GetRules(MenuRuleKind.Suggestion))
        {
            var count = CountTagged(rule.TargetTag, true, null);
            var guests = TagRules.Guests(rule.Basis ?? GuestBasis.Guests, visitState.Adults, visitState.Children);
            var shortage = TagRules.Shortage(count, guests);
            if ((count == 0) || (shortage == 0))
            {
                continue;
            }

            // 確認の画面の中でそのまま入れられる品 (必須のオプションと確認のルールがない) だけを出す
            var items = (rule.SuggestItemIds ?? [])
                .Select(menuState.GetItem)
                .Where(x => !menuState.IsSoldOut(x.Id) &&
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
}
