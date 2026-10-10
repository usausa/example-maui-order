namespace TableOrder.Terminal.Models.Input;

#pragma warning disable IDE0032
// 電卓の入力 (数と番号。小数は入れない)
public sealed class NumberInputModel : NotificationObject
{
    private string text = "0";

    public int MaxLength { get; set; }

    public bool AllowEmpty { get; set; }

    // 番号 (電話・郵便番号・コード) は先頭の 0 を残す
    public bool KeepLeadingZeros { get; set; }

    // PIN は入力した桁数だけを見せる
    public bool Masked { get; set; }

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

        Text = (text == "0") && !KeepLeadingZeros ? key : text + key;
    }
}
#pragma warning restore IDE0032
