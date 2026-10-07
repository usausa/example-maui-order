namespace TableOrder.Terminal.Table.Models.Input;

#pragma warning disable IDE0032
public sealed class NumberInputModel : NotificationObject
{
    private string text = "0";

    public int MaxLength { get; set; }

    public int Scale { get; set; }

    public bool AllowEmpty { get; set; }

    // 番号 (電話・郵便番号・コード) は先頭の 0 を残す
    public bool KeepLeadingZeros { get; set; }

    // PIN は入力した桁数だけを見せる
    public bool Masked { get; set; }

    private int IntegerLength => Scale > 0 ? MaxLength - Scale - 1 : MaxLength;

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, String.IsNullOrEmpty(value) ? AllowEmpty ? string.Empty : "0" : value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => Masked ? new string('●', text.Length) : text;

    public string NormalizeText => text.EndsWith('.') ? text[..^1] : text;

    public void Clear()
    {
        Text = AllowEmpty ? string.Empty : "0";
    }

    public void Pop()
    {
        Text = text.Length > 1 ? text[..^1] : AllowEmpty ? string.Empty : "0";
    }

    public void Push(string key)
    {
        if (text.Length + key.Length > MaxLength)
        {
            return;
        }

        if (key == ".")
        {
            if (String.IsNullOrEmpty(text) || (text == "0"))
            {
                Text = "0.";
            }
            else if (text.IndexOf('.', StringComparison.Ordinal) < 0)
            {
                Text = text + ".";
            }
        }
        else
        {
            var index = text.IndexOf('.', StringComparison.Ordinal);
            if (index >= 0)
            {
                if (text.Length - index <= Scale)
                {
                    Text = text + key;
                }
            }
            else
            {
                if ((text == "0") && !KeepLeadingZeros)
                {
                    Text = key;
                }
                else if (text.Length + key.Length <= IntegerLength)
                {
                    Text = text + key;
                }
            }
        }
    }
}
#pragma warning restore IDE0032
