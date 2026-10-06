namespace TableOrder.Domain.Enums;

// ルールを数える範囲 (確認は Visit なら来店で 1 回、Order なら注文ごと。上限は注文・来店・1 人あたり)
public enum RuleScope
{
    Order,
    Visit,
    Guest
}
