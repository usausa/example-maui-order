namespace TableOrder.KitchenApp.Components.Controls;

// 数を入れる部品 (物理キーボードを前提にしないので、画面のボタンで入れる)
public sealed partial class NumberPad
{
    private const string Digits = "123456789";

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public int MaxLength { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    private Task AppendAsync(char digit) =>
        Value.Length < MaxLength ? ValueChanged.InvokeAsync(Value + digit) : Task.CompletedTask;

    private Task DeleteAsync() =>
        Value.Length > 0 ? ValueChanged.InvokeAsync(Value[..^1]) : Task.CompletedTask;

    private Task ClearAsync() =>
        ValueChanged.InvokeAsync(string.Empty);
}
