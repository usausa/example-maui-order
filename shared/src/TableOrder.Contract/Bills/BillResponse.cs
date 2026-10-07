namespace TableOrder.Contract.Bills;

// 会計の明細と合計 (同じ商品とオプションの明細はまとめ、取消を除く)
public sealed class BillResponse
{
    public Guid VisitId { get; set; }

    // 明細が変わると変わる (会計を始めるときに確かめる)
    public string BillVersion { get; set; } = default!;

    public IReadOnlyList<BillResponseLine> Lines { get; set; } = default!;

    // 税率ごとの対象額と税額 (内税)
    public IReadOnlyList<BillResponseTax> Taxes { get; set; } = default!;

    public decimal Total { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal Balance { get; set; }

    public int Guests { get; set; }

    // 人数で割った目安
    public IReadOnlyList<decimal> SplitAmounts { get; set; } = default!;

    // まだ出していない品がある
    public bool HasUnservedLines { get; set; }
}

public sealed class BillResponseLine
{
    public LocalizedText Name { get; set; } = default!;

    public IReadOnlyList<LocalizedText> Options { get; set; } = default!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxRate { get; set; }
}

public sealed class BillResponseTax
{
    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}
