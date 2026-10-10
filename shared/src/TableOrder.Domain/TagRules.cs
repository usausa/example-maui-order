namespace TableOrder.Domain;

using TableOrder.Domain.Enums;

// タグに対するメニューのルール (MenuRuleKind) の数え方。端末は注文の前の案内に、サーバは注文の確認に使う
public static class TagRules
{
    // ルールで比べる人数
    public static int Guests(GuestBasis basis, int adults, int children) =>
        basis switch
        {
            GuestBasis.Adults => adults,
            GuestBasis.Children => children,
            _ => adults + children
        };

    // 提案 (Suggestion): 人数に足りない数
    public static int Shortage(int count, int guests) =>
        Math.Max(0, guests - count);

    // 上限 (Limit): 範囲の中で入れられる数 (Guest は 1 人あたりの上限 × 人数)
    public static int Allowance(RuleScope scope, int max, int guests) =>
        scope == RuleScope.Guest ? max * Math.Max(1, guests) : max;

    // 出せる条件 (Availability) を満たさない理由 (満たせば None)。時間帯があればどれかの中で、子どもを求めるなら子どもが 1 人以上いる
    // 時間帯の終わりから grace の間は中とする (サーバが、確定を押したあとの通信の遅れを見込む)
    public static UnavailableReason CheckAvailability(IEnumerable<(TimeOnly Start, TimeOnly End)> periods, bool requiresChildren, TimeOnly now, int children, TimeSpan grace = default)
    {
        if (requiresChildren && (children < 1))
        {
            return UnavailableReason.Children;
        }

        var limited = false;
        foreach (var (start, end) in periods)
        {
            if (StoreHours.InPeriod(now, start, end, grace))
            {
                return UnavailableReason.None;
            }

            limited = true;
        }

        return limited ? UnavailableReason.Daypart : UnavailableReason.None;
    }
}
