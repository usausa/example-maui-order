namespace TableOrder.Terminal.Models.Input;

// 番号の入力 (PIN、ペアリングコード。先頭の 0 を残し、空も許す)
public sealed class DigitsInputParameter
{
    public string Title { get; }

    public string Value { get; }

    public int MaxLength { get; }

    // true: 入力を伏せて表示する (PIN)
    public bool Masked { get; }

    public DigitsInputParameter(string title, string value, int maxLength, bool masked = false)
    {
        Title = title;
        Value = value;
        MaxLength = maxLength;
        Masked = masked;
    }
}
