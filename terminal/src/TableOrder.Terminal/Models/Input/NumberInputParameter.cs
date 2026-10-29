namespace TableOrder.Terminal.Models.Input;

// 数の入力 (空は 0 にし、先頭の 0 は残さない)。番号と PIN は DigitsInputParameter
public sealed class NumberInputParameter
{
    public string Title { get; }

    public string Value { get; }

    public int MaxLength { get; }

    public NumberInputParameter(string title, string value, int maxLength)
    {
        Title = title;
        Value = value;
        MaxLength = maxLength;
    }
}
