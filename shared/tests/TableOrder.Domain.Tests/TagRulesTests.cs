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

    // 出せる条件の時間帯 (朝 7:00〜10:30、ランチ 11:00〜15:00) は、どれかの中なら出せる。猶予 (2 分) は終わりのあとに付ける
    [Theory]
    [InlineData("06:59", 0, UnavailableReason.Daypart)]
    [InlineData("08:00", 0, UnavailableReason.None)]
    [InlineData("10:45", 0, UnavailableReason.Daypart)]
    [InlineData("12:00", 0, UnavailableReason.None)]
    [InlineData("15:01", 0, UnavailableReason.Daypart)]
    [InlineData("15:01", 2, UnavailableReason.None)]
    [InlineData("15:02", 2, UnavailableReason.Daypart)]
    public void AvailableInAnyPeriod(string now, int graceMinutes, UnavailableReason expected)
    {
        // Arrange
        var periods = new[] { (StoreHours.Parse("07:00"), StoreHours.Parse("10:30")), (StoreHours.Parse("11:00"), StoreHours.Parse("15:00")) };

        // Act
        var reason = TagRules.CheckAvailability(periods, false, StoreHours.Parse(now), 0, TimeSpan.FromMinutes(graceMinutes));

        // Assert
        Assert.Equal(expected, reason);
    }

    // 子どもを求める品は子どもが 1 人以上の来店だけ。時間帯がなければ時刻によらない
    [Theory]
    [InlineData(0, UnavailableReason.Children)]
    [InlineData(1, UnavailableReason.None)]
    [InlineData(3, UnavailableReason.None)]
    public void AvailableWithChildren(int children, UnavailableReason expected)
    {
        var reason = TagRules.CheckAvailability([], true, StoreHours.Parse("03:00"), children);

        Assert.Equal(expected, reason);
    }

    // 時間帯と子どもの両方を求める品は、両方を満たすときだけ出せる (どちらも満たさなければ子どもを理由にする)
    [Theory]
    [InlineData("12:00", 1, UnavailableReason.None)]
    [InlineData("12:00", 0, UnavailableReason.Children)]
    [InlineData("16:00", 1, UnavailableReason.Daypart)]
    [InlineData("16:00", 0, UnavailableReason.Children)]
    public void AvailableNeedsAllConditions(string now, int children, UnavailableReason expected)
    {
        var reason = TagRules.CheckAvailability([(StoreHours.Parse("11:00"), StoreHours.Parse("15:00"))], true, StoreHours.Parse(now), children);

        Assert.Equal(expected, reason);
    }

    // 空の時間帯 (メニューにない時間帯のコード) だけを指す品は、猶予があっても出さない
    [Fact]
    public void EmptyPeriodIsNeverAvailable()
    {
        var reason = TagRules.CheckAvailability([(TimeOnly.MinValue, TimeOnly.MinValue)], false, TimeOnly.MinValue.AddMinutes(1), 1, TimeSpan.FromMinutes(2));

        Assert.Equal(UnavailableReason.Daypart, reason);
    }
}
