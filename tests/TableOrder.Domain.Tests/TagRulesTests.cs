namespace TableOrder.Domain;

public sealed class TagRulesTests
{
    // ルールで比べる人数は、基準に合わせて大人・子ども・合計から選ぶ (大人 2 人、子ども 1 人)
    [Theory]
    [InlineData(GuestBasis.Guests, 3)]
    [InlineData(GuestBasis.Adults, 2)]
    [InlineData(GuestBasis.Children, 1)]
    public void GuestsByBasis(GuestBasis basis, int expected)
    {
        var guests = TagRules.Guests(basis, 2, 1);

        Assert.Equal(expected, guests);
    }

    // 提案は人数に足りない数にし、足りていれば (多くても) 0
    [Theory]
    [InlineData(1, 3, 2)]
    [InlineData(3, 3, 0)]
    [InlineData(5, 3, 0)]
    public void ShortageOfGuests(int count, int guests, int expected)
    {
        var shortage = TagRules.Shortage(count, guests);

        Assert.Equal(expected, shortage);
    }

    // 上限は、1 人あたり (Guest) なら人数を掛け、注文ごと・来店ごとならそのまま (1 点まで、3 人)
    [Theory]
    [InlineData(RuleScope.Guest, 3)]
    [InlineData(RuleScope.Order, 1)]
    [InlineData(RuleScope.Visit, 1)]
    public void AllowanceByScope(RuleScope scope, int expected)
    {
        var allowance = TagRules.Allowance(scope, 1, 3);

        Assert.Equal(expected, allowance);
    }

    // 人数が 0 でも、1 人あたりの上限は 1 人分を認める
    [Fact]
    public void AllowanceForNoGuests()
    {
        var allowance = TagRules.Allowance(RuleScope.Guest, 2, 0);

        Assert.Equal(2, allowance);
    }
}
