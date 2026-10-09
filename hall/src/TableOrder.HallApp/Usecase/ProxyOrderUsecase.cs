namespace TableOrder.HallApp.Usecase;

// 代わりの注文。メニューのルール (確認・上限) の判定、確認の記録、注文の送信 (カートは画面が持ち、判定に渡す)
// 数え方はテーブル端末とサーバと同じ (TagRules)
public sealed class ProxyOrderUsecase
{
    private readonly MenuState menuState;

    private readonly IHallApi hallApi;

    // 送れたかわからなかった注文 (同じ来店の次の送信で、同じ注文の id を送り直す)
    private (Guid VisitId, Guid OrderId)? pendingOrder;

    public ProxyOrderUsecase(
        MenuState menuState,
        IHallApi hallApi)
    {
        this.menuState = menuState;
        this.hallApi = hallApi;
    }

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

    // カートを注文として送る (サーバは同じ id の送り直しに、受けた注文を返す)
    public async ValueTask<ApiResult<OrderListResponseItem>> SubmitAsync(VisitResponse visit, IEnumerable<CartLine> cart)
    {
        var id = (pendingOrder is { } pending) && (pending.VisitId == visit.Id) ? pending.OrderId : Guid.CreateVersion7();
        var request = new OrderCreateRequest
        {
            Id = id,
            MenuVersion = menuState.Menu.MenuVersion,
            Lines = cart
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

        var result = await hallApi.CreateOrderAsync(visit.Id, request);
        pendingOrder = result.Status == ApiStatus.Unavailable ? (visit.Id, id) : null;
        return result;
    }
}
