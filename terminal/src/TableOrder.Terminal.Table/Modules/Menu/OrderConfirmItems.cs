namespace TableOrder.Terminal.Table.Modules.Menu;

// 注文の確認の明細
public sealed class ConfirmLine
{
    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public bool IsAfterMeal { get; }

    public string QuantityText { get; }

    public string AmountText { get; }

    public ConfirmLine(string name, string optionText, CartLine line)
    {
        Name = name;
        OptionText = optionText;
        IsAfterMeal = line.Timing == OrderTiming.AfterMeal;
        QuantityText = $"× {line.Quantity}";
        AmountText = ViewHelper.Price(line.Amount);
    }
}

// 提案する商品 (ドリンクバーなど)
public sealed class SuggestItem
{
    public Guid Id { get; }

    public string Name { get; }

    public string PriceText { get; }

    public SuggestItem(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        PriceText = ViewHelper.Price(price);
    }
}
