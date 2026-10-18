namespace TableOrder.Terminal.Behaviors;

public static partial class Scroll
{
    public static partial void UseCustomMapper(BehaviorOptions options);

    // スクロールの端の効果 (伸びる、光る) を出さない。アプリ全体で止めるので既定を true にし、効果を出す要素だけ false にする (ScrollView と ItemsView に効く)
    public static readonly BindableProperty DisableOverScrollProperty = BindableProperty.CreateAttached(
        "DisableOverScroll",
        typeof(bool),
        typeof(Scroll),
        true);

    public static bool GetDisableOverScroll(BindableObject bindable) => (bool)bindable.GetValue(DisableOverScrollProperty);

    public static void SetDisableOverScroll(BindableObject bindable, bool value) => bindable.SetValue(DisableOverScrollProperty, value);
}
