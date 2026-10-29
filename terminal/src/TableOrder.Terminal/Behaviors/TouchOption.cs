namespace TableOrder.Terminal.Behaviors;

// 押せる行とタイルの波紋とクリック (要素を増やさずに、付けたビューの前景に波紋を描き、標準のクリックで受ける)
// 子のボタンは自分でクリックを受けるので、ボタンを重ねた面 (メニューのカードの +) の親に付けても、ボタンのタップでは実行しない
public static partial class TouchOption
{
    public static partial void UseCustomMapper(BehaviorOptions options);

    // 押したときの波紋
    public static readonly BindableProperty RippleProperty = BindableProperty.CreateAttached(
        "Ripple",
        typeof(bool),
        typeof(TouchOption),
        false);

    public static bool GetRipple(BindableObject bindable) => (bool)bindable.GetValue(RippleProperty);

    public static void SetRipple(BindableObject bindable, bool value) => bindable.SetValue(RippleProperty, value);

    // 波紋の色。既定は黒に近い色を薄くしたもの (明るい面の上で薄い灰色に見え、チェーンの色によらない)
    public static readonly BindableProperty RippleColorProperty = BindableProperty.CreateAttached(
        "RippleColor",
        typeof(Color),
        typeof(TouchOption),
        Color.FromArgb("#2E1C1B1F"));

    public static Color GetRippleColor(BindableObject bindable) => (Color)bindable.GetValue(RippleColorProperty);

    public static void SetRippleColor(BindableObject bindable, Color value) => bindable.SetValue(RippleColorProperty, value);

    // 押して離したときに実行するコマンド (実行できないとき (処理中、ポップアップを開いている間) は何もしない)
    public static readonly BindableProperty ClickCommandProperty = BindableProperty.CreateAttached(
        "ClickCommand",
        typeof(ICommand),
        typeof(TouchOption),
        null);

    public static ICommand? GetClickCommand(BindableObject bindable) => (ICommand?)bindable.GetValue(ClickCommandProperty);

    public static void SetClickCommand(BindableObject bindable, ICommand? value) => bindable.SetValue(ClickCommandProperty, value);

    public static readonly BindableProperty ClickCommandParameterProperty = BindableProperty.CreateAttached(
        "ClickCommandParameter",
        typeof(object),
        typeof(TouchOption),
        null);

    public static object? GetClickCommandParameter(BindableObject bindable) => bindable.GetValue(ClickCommandParameterProperty);

    public static void SetClickCommandParameter(BindableObject bindable, object? value) => bindable.SetValue(ClickCommandParameterProperty, value);
}
