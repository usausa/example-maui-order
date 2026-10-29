namespace TableOrder.Terminal.Markup;

// 文言の前に色付きの絵文字を添える (絵文字は言語によらないので resx に入れず、画面で添える)
[AcceptEmptyServiceProvider]
[ContentProperty("Value")]
public sealed class EmojiExtension : IMarkupExtension<string>
{
    public string Value { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => $"{Value} {Text}";

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
