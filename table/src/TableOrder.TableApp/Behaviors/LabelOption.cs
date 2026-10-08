namespace TableOrder.TableApp.Behaviors;

public static partial class LabelOption
{
    public static partial void UseCustomMapper(BehaviorOptions options);

    // ReSharper disable InconsistentNaming
    public static readonly BindableProperty AutoSizeProperty = BindableProperty.CreateAttached(
        "AutoSize",
        typeof(bool),
        typeof(LabelOption),
        false);
    // ReSharper restore InconsistentNaming

    public static bool GetAutoSize(BindableObject bindable) => (bool)bindable.GetValue(AutoSizeProperty);

    public static void SetAutoSize(BindableObject bindable, bool value) => bindable.SetValue(AutoSizeProperty, value);

    // ReSharper disable InconsistentNaming
    public static readonly BindableProperty MaxSizeProperty = BindableProperty.CreateAttached(
        "MaxSize",
        typeof(double),
        typeof(LabelOption),
        144d);
    // ReSharper restore InconsistentNaming

    public static double GetMaxSize(BindableObject bindable) => (double)bindable.GetValue(MaxSizeProperty);

    public static void SetMaxSize(BindableObject bindable, double value) => bindable.SetValue(MaxSizeProperty, value);

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
