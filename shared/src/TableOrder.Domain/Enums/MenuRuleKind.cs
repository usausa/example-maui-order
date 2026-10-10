namespace TableOrder.Domain.Enums;

// メニューのルールの種類。特定の商品 (ドリンクバー、お酒など) をコードで分けず、タグに対するルールで表す
public enum MenuRuleKind
{
    // タグの品が人数より少なければ、注文の確認で提案する
    Suggestion,
    // タグの品を入れるときに確かめる
    Confirmation,
    // タグの品の数の上限
    Limit,
    // タグの品とカテゴリを出せる条件 (時間帯のどれかの中、子どもがいる)。満たさないときは出さず、受け付けない
    Availability
}
