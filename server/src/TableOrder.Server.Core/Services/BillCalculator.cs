namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Contract.Bills;

// 会計の明細と合計 (会計の画面、会計の開始、支払の額の確かめで同じ計算を使う)
internal static class BillCalculator
{
    public static BillResponse Calculate(VisitEntity visit, StoreEntity store, IEnumerable<OrderLineEntity> lines, IEnumerable<OrderLineOptionEntity> options, IEnumerable<PaymentEntity> payments)
    {
        var active = lines.Where(static x => x.Status != OrderLineStatus.Cancelled).ToList();
        var optionsByLine = options.ToLookup(static x => x.LineId);

        // 同じ商品・オプション・単価の明細はまとめる (オプションは選んだ順)
        var billLines = active
            .GroupBy(x => (x.ItemId, Options: String.Join(',', optionsByLine[x.Id].Select(static o => o.OptionId)), x.UnitPrice))
            .Select(g =>
            {
                var first = g.First();
                var quantity = g.Sum(static x => x.Quantity);
                return new BillResponseLine
                {
                    Name = first.Name,
                    Options = optionsByLine[first.Id].Select(static x => x.Name).ToList(),
                    Quantity = quantity,
                    UnitPrice = first.UnitPrice,
                    Amount = first.UnitPrice * quantity,
                    TaxRate = first.TaxRate
                };
            })
            .ToList();
        var taxes = billLines
            .GroupBy(static x => x.TaxRate)
            .Select(g =>
            {
                var taxable = g.Sum(static x => x.Amount);
                return new BillResponseTax
                {
                    Rate = g.Key,
                    TaxableAmount = taxable,
                    TaxAmount = Pricing.IncludedTax(taxable, g.Key, store.TaxRounding)
                };
            })
            .ToList();
        var total = billLines.Sum(static x => x.Amount);
        var paid = PaidAmount(payments);
        var guests = visit.Adults + visit.Children;
        return new BillResponse
        {
            VisitId = visit.Id,
            BillVersion = VersionOf(active),
            Lines = billLines,
            Taxes = taxes,
            Total = total,
            PaidAmount = paid,
            Balance = total - paid,
            Guests = guests,
            SplitAmounts = Pricing.Split(total, guests),
            HasUnservedLines = active.Any(static x => x.Status != OrderLineStatus.Served)
        };
    }

    // 払い終えた額
    public static decimal PaidAmount(IEnumerable<PaymentEntity> payments) =>
        payments.Where(static x => x.Status == PaymentStatus.Completed).Sum(static x => x.Amount);

    // 明細の版。取消を除いた明細 (Id、数量、単価) から求め、列には持たない
    private static string VersionOf(IEnumerable<OrderLineEntity> lines)
    {
        var text = String.Join('\n', lines.OrderBy(static x => x.Id).Select(static x => String.Create(CultureInfo.InvariantCulture, $"{x.Id:N}:{x.Quantity}:{x.UnitPrice}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16));
    }
}
