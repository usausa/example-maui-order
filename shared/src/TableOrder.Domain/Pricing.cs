namespace TableOrder.Domain;

using TableOrder.Domain.Enums;

// 金額の計算 (価格は税込)。端末は表示のために、サーバは注文と会計の確認に同じ計算を使う
public static class Pricing
{
    // 明細の単価 = 商品の価格 + 選んだオプションの差額
    public static decimal UnitPrice(decimal price, IEnumerable<decimal> priceDeltas) =>
        price + priceDeltas.Sum();

    // 税込の額に含まれる税 (内税)
    public static decimal IncludedTax(decimal amount, decimal rate, TaxRounding rounding)
    {
        var tax = amount * rate / (1 + rate);
        return rounding switch
        {
            TaxRounding.Round => Math.Round(tax, MidpointRounding.AwayFromZero),
            TaxRounding.Ceiling => Math.Ceiling(tax),
            _ => Math.Floor(tax)
        };
    }

    // 人数で割った目安。割り切れない分は先頭の人から 1 円ずつ足す
    public static IReadOnlyList<decimal> Split(decimal total, int guests)
    {
        if (guests <= 1)
        {
            return [total];
        }

        var unit = Math.Floor(total / guests);
        var remainder = total - (unit * guests);
        var amounts = new decimal[guests];
        for (var i = 0; i < guests; i++)
        {
            amounts[i] = i < remainder ? unit + 1 : unit;
        }

        return amounts;
    }
}
