namespace TableOrder.Domain;

public sealed class PricingTests
{
    //--------------------------------------------------------------------------------
    // UnitPrice
    //--------------------------------------------------------------------------------

    // 単価は商品の価格に選んだオプションの差額を足す (999 円の品にセット 330 円とドリンクバー 299 円)
    [Fact]
    public void UnitPriceAddsOptionDeltas()
    {
        var price = Pricing.UnitPrice(999m, [330m, 299m]);

        Assert.Equal(1628m, price);
    }

    // オプションを選ばなければ商品の価格のまま
    [Fact]
    public void UnitPriceWithoutOptions()
    {
        var price = Pricing.UnitPrice(999m, []);

        Assert.Equal(999m, price);
    }

    //--------------------------------------------------------------------------------
    // IncludedTax
    //--------------------------------------------------------------------------------

    // 内税は税込の額から割り戻し、端数は店舗の指定で丸める (4,414 円は 401.27…、1,000 円は 90.90…)
    [Theory]
    [InlineData(4414, TaxRounding.Floor, 401)]
    [InlineData(4414, TaxRounding.Round, 401)]
    [InlineData(4414, TaxRounding.Ceiling, 402)]
    [InlineData(1000, TaxRounding.Floor, 90)]
    [InlineData(1000, TaxRounding.Round, 91)]
    [InlineData(1000, TaxRounding.Ceiling, 91)]
    public void IncludedTaxRoundsBySetting(int amount, TaxRounding rounding, int expected)
    {
        var tax = Pricing.IncludedTax(amount, 0.10m, rounding);

        Assert.Equal(expected, tax);
    }

    // 軽減税率 (8%) も同じ式で割り戻す
    [Fact]
    public void IncludedTaxReducedRate()
    {
        var tax = Pricing.IncludedTax(1080m, 0.08m, TaxRounding.Floor);

        Assert.Equal(80m, tax);
    }

    //--------------------------------------------------------------------------------
    // Split
    //--------------------------------------------------------------------------------

    // 割り切れない分は先頭の人から 1 円ずつ足す
    [Fact]
    public void SplitAddsRemainderFromFirst()
    {
        var amounts = Pricing.Split(4414m, 3);

        Assert.Equal([1472m, 1471m, 1471m], amounts);
    }

    // 割り切れるときは同じ額
    [Fact]
    public void SplitEvenly()
    {
        var amounts = Pricing.Split(4000m, 4);

        Assert.Equal([1000m, 1000m, 1000m, 1000m], amounts);
    }

    // 1 人以下なら分けない
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void SplitSingleGuest(int guests)
    {
        var amounts = Pricing.Split(4414m, guests);

        Assert.Equal([4414m], amounts);
    }

    // 額が人数より少ないときは、払わない人がいる
    [Fact]
    public void SplitLessThanGuests()
    {
        var amounts = Pricing.Split(2m, 3);

        Assert.Equal([1m, 1m, 0m], amounts);
    }

    // 1 人分ずつ払い、残りを残りの人数で割り直しても、初めに割った額と同じになる (お会計で分けて払う流れ)
    [Theory]
    [InlineData(4414, 3)]
    [InlineData(1298, 3)]
    [InlineData(10000, 7)]
    [InlineData(5, 4)]
    public void SplitAgainAfterEachPayment(int total, int guests)
    {
        // Arrange
        var expected = Pricing.Split(total, guests);

        // Act
        var paid = new List<decimal>();
        var balance = (decimal)total;
        for (var remaining = guests; remaining > 0; remaining--)
        {
            var amount = Pricing.Split(balance, remaining)[0];
            paid.Add(amount);
            balance -= amount;
        }

        // Assert
        Assert.Equal(expected, paid);
        Assert.Equal(0m, balance);
    }
}
