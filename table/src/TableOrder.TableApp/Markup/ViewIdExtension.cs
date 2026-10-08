namespace TableOrder.TableApp.Markup;

using System;

using TableOrder.TableApp.Modules;

[AcceptEmptyServiceProvider]
[ContentProperty("Value")]
public sealed class ViewIdExtension : IMarkupExtension<ViewId>
{
    public ViewId Value { get; set; }

    public ViewId ProvideValue(IServiceProvider serviceProvider) => Value;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
