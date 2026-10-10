namespace TableOrder.Terminal.Behaviors;

public static partial class LabelOption
{
    public static partial void UseCustomMapper(BehaviorOptions options);

    // 行の高さ (dp。0 は指定なし)。英字と和文で同じ行の高さにして、ほかの要素を行の位置にそろえるときに使う
    // Label.LineHeight (倍率) と名前を分ける (同じ名前だと MAUI の対応付けに混ざり、装飾付きの文字で飛ばされる)
    // ReSharper disable InconsistentNaming
    public static readonly BindableProperty FixedLineHeightProperty = BindableProperty.CreateAttached(
        "FixedLineHeight",
        typeof(double),
        typeof(LabelOption),
        0d);
    // ReSharper restore InconsistentNaming

    public static double GetFixedLineHeight(BindableObject bindable) => (double)bindable.GetValue(FixedLineHeightProperty);

    public static void SetFixedLineHeight(BindableObject bindable, double value) => bindable.SetValue(FixedLineHeightProperty, value);
}
