namespace TableOrder.Domain.Enums;

// メニューのルールの種類。特定の商品 (ドリンクバー、お酒など) をコードで分けず、タグに対するルールで表す
public enum MenuRuleKind
{
    // タグの品が人数より少なければ、注文の確認で提案する
    Suggestion,
    // タグの品を入れるときに確かめる
    Confirmation,
    // タグの品の数の上限
    Limit
}
