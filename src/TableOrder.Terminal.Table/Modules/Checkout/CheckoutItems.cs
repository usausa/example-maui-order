namespace TableOrder.Terminal.Table.Modules.Checkout;

// お会計の段階
public enum CheckoutStep
{
    Loading,
    Method,
    QrCode,
    CreditCard,
    Register,
    Completed
}

// お会計の明細 (同じ商品とオプションはまとめてある)
public sealed class CheckoutLine
{
    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public string QuantityText { get; }

    public string AmountText { get; }

    public CheckoutLine(BillResponseLine line, Language language)
    {
        Name = line.Name.Get(language);
        OptionText = String.Join(" / ", line.Options.Select(x => x.Get(language)));
        QuantityText = $"× {line.Quantity}";
        AmountText = ViewHelper.Price(line.Amount);
    }
}
