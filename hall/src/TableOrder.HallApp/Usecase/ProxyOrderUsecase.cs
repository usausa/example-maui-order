namespace TableOrder.HallApp.Usecase;

// 代わりの注文。メニューのルール (出せる条件・確認・上限) の判定、確認の記録、注文の送信 (カートは画面が持ち、判定に渡す)
// 数え方はテーブル端末とサーバと同じ (TagRules)
public sealed class ProxyOrderUsecase
{
    private readonly MenuState menuState;

    private readonly StoreState storeState;

    private readonly IHallApi hallApi;

    // 送れたかわからなかった注文 (同じ来店で同じカートを送るときだけ、同じ注文の id で送り直す。カートを替えたら新しい注文にする)
    private (Guid VisitId, OrderCreateRequest Request)? pendingOrder;

    public ProxyOrderUsecase(
        MenuState menuState,
        StoreState storeState,
        IHallApi hallApi)
    {
        this.menuState = menuState;
        this.storeState = storeState;
        this.hallApi = hallApi;
    }

    //--------------------------------------------------------------------------------
    // Availability
    //--------------------------------------------------------------------------------

    // 来店の出し分け (端末の時計を店舗の現地時刻にし、来店の子どもの人数で求める)
    public MenuAvailability GetAvailability(VisitResponse visit) =>
        MenuAvailability.Evaluate(menuState.Menu, StoreHours.LocalTime(DateTimeOffset.UtcNow, storeState.Store.TimeZone), visit.Children);

    // 商品と選んだオプションが出せる条件を満たさない理由 (満たせば None)
    public UnavailableReason FindUnavailable(MenuAvailability availability, Guid itemId, IEnumerable<Guid> optionIds) =>
        availability.ReasonOf(menuState.GetTags(itemId, optionIds));

    //--------------------------------------------------------------------------------
    // Rule
    //--------------------------------------------------------------------------------

    // 入れる前にお客様に確かめるルール (来店で 1 回のルールは、記録したものを除く)
    public IReadOnlyList<MenuResponseRule> GetRequiredConfirmations(VisitResponse visit, ItemSelection selection)
    {
        var tags = menuState.GetTags(selection.ItemId, selection.OptionIds);
        return menuState.GetRules(MenuRuleKind.Confirmation)
            .Where(x => tags.Contains(x.TargetTag) && !((x.Scope == RuleScope.Visit) && visit.ConfirmedRuleIds.Contains(x.Id)))
            .ToList();
    }

    // スタッフがお客様に確かめた確認のルールを来店に記録する
    public ValueTask<ApiResult<VisitResponse>> ConfirmAsync(VisitResponse visit, MenuResponseRule rule) =>
        hallApi.ConfirmAsync(visit.Id, new VisitConfirmationRequest { RuleId = rule.Id });

    // 上限を超えるルール。注文ごとのルールはカートだけ、ほかは来店の注文 (取消を除く) も合わせて数える
    public MenuResponseRule? FindExceededLimit(VisitResponse visit, IReadOnlyList<OrderListResponseLine> ordered, IReadOnlyList<CartLine> cart, ItemSelection selection)
    {
        var tags = menuState.GetTags(selection.ItemId, selection.OptionIds);
        foreach (var rule in menuState.GetRules(MenuRuleKind.Limit))
        {
            if (!tags.Contains(rule.TargetTag) || (rule.Max is not { } max))
            {
                continue;
            }

            var scope = rule.Scope ?? RuleScope.Order;
            var count = selection.Quantity + cart
                .Where(x => menuState.GetTags(x.ItemId, x.OptionIds).Contains(rule.TargetTag))
                .Sum(static x => x.Quantity);
            if (scope != RuleScope.Order)
            {
                count += ordered
                    .Where(x => (x.Status != OrderLineStatus.Cancelled) && menuState.GetTags(x.ItemId, x.Options.Select(static o => o.OptionId)).Contains(rule.TargetTag))
                    .Sum(static x => x.Quantity);
            }

            if (count > TagRules.Allowance(scope, max, visit.Adults + visit.Children))
            {
                return rule;
            }
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 送れたかわからなかった注文の送り直しになる (同じ来店の同じカート)。送り直しは出せる条件で止めない (届いていれば受けた注文が返る)
    public bool IsResend(VisitResponse visit, IEnumerable<CartLine> cart) =>
        (pendingOrder is { } pending) && (pending.VisitId == visit.Id) && IsSameLines(pending.Request.Lines, ToLines(cart));

    // カートを注文として送る (サーバは同じ id の送り直しに、受けた注文を返す)
    public async ValueTask<ApiResult<OrderListResponseItem>> SubmitAsync(VisitResponse visit, IEnumerable<CartLine> cart)
    {
        var lines = ToLines(cart);
        var resend = (pendingOrder is { } pending) && (pending.VisitId == visit.Id) && IsSameLines(pending.Request.Lines, lines);
        var request = new OrderCreateRequest
        {
            Id = resend ? pendingOrder!.Value.Request.Id : Guid.CreateVersion7(),
            MenuVersion = menuState.Menu.MenuVersion,
            Lines = lines
        };

        var result = await hallApi.CreateOrderAsync(visit.Id, request);
        pendingOrder = result.Status == ApiStatus.Unavailable ? (visit.Id, request) : null;
        return result;
    }

    private static List<OrderCreateRequestLine> ToLines(IEnumerable<CartLine> cart) =>
        cart
            .Select(static x => new OrderCreateRequestLine
            {
                Id = x.Id,
                ItemId = x.ItemId,
                OptionIds = x.OptionIds,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Timing = x.Timing
            })
            .ToList();

    private static bool IsSameLines(IReadOnlyList<OrderCreateRequestLine> previous, List<OrderCreateRequestLine> lines) =>
        (previous.Count == lines.Count) &&
        previous.Zip(lines).All(static x =>
            (x.First.Id == x.Second.Id) &&
            (x.First.ItemId == x.Second.ItemId) &&
            (x.First.Quantity == x.Second.Quantity) &&
            (x.First.UnitPrice == x.Second.UnitPrice) &&
            (x.First.Timing == x.Second.Timing) &&
            x.First.OptionIds.SequenceEqual(x.Second.OptionIds));
}
