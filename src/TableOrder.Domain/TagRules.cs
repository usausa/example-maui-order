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
}
