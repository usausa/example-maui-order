namespace TableOrder.TableApp.Behaviors;

// 横にスクロールする ScrollView の端に重ねる送りのボタン
// 続きがある向きのときだけ出して続きがあることを示し、押すとその向きへ見えている幅の 3/4 だけ送る (前のタブを少し残す)
// 送る ScrollView は XAML で一度だけ決める (付け替えは扱わない)
public static class ScrollPager
{
    private const double Tolerance = 1;

    private const double PageRate = 0.75;

    // ReSharper disable InconsistentNaming
    public static readonly BindableProperty TargetProperty = BindableProperty.CreateAttached(
        "Target",
        typeof(ScrollView),
        typeof(ScrollPager),
        null,
        propertyChanged: HandleTargetChanged);
    // ReSharper restore InconsistentNaming

    public static ScrollView? GetTarget(BindableObject bindable) => (ScrollView?)bindable.GetValue(TargetProperty);

    public static void SetTarget(BindableObject bindable, ScrollView? value) => bindable.SetValue(TargetProperty, value);

    // 送る向き (false は前へ)
    // ReSharper disable InconsistentNaming
    public static readonly BindableProperty ForwardProperty = BindableProperty.CreateAttached(
        "Forward",
        typeof(bool),
        typeof(ScrollPager),
        true,
        propertyChanged: HandleForwardChanged);
    // ReSharper restore InconsistentNaming

    public static bool GetForward(BindableObject bindable) => (bool)bindable.GetValue(ForwardProperty);

    public static void SetForward(BindableObject bindable, bool value) => bindable.SetValue(ForwardProperty, value);

    private static void HandleTargetChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if ((bindable is not View view) || (newValue is not ScrollView target))
        {
            return;
        }

        // 続きの有無は、送ったとき、大きさが決まったとき、中身 (タブ) が変わったときに見直す
        target.Scrolled += (_, _) => Update(view, target);
        target.SizeChanged += (_, _) => Update(view, target);
        target.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == ScrollView.ContentSizeProperty.PropertyName)
            {
                Update(view, target);
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _ = PageAsync(view, target);
        view.GestureRecognizers.Add(tap);

        Update(view, target);
    }

    private static void HandleForwardChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if ((bindable is View view) && (GetTarget(view) is { } target))
        {
            Update(view, target);
        }
    }

    private static void Update(View view, ScrollView target)
    {
        view.IsVisible = GetForward(view)
            ? target.ScrollX + target.Width < target.ContentSize.Width - Tolerance
            : target.ScrollX > Tolerance;
    }

    private static Task PageAsync(View view, ScrollView target)
    {
        var step = target.Width * PageRate;
        var x = GetForward(view)
            ? Math.Min(target.ScrollX + step, target.ContentSize.Width - target.Width)
            : Math.Max(target.ScrollX - step, 0);
        return target.ScrollToAsync(x, 0, true);
    }
}
