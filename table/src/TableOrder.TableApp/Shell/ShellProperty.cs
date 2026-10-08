namespace TableOrder.TableApp.Shell;

using Microsoft.Maui.Controls.Shapes;

using Smart.Maui.Interactivity;

public static class ShellProperty
{
    // ------------------------------------------------------------
    // BusyOverlay
    // ------------------------------------------------------------

    // 処理中は画面全体を透明な面で覆い、タッチを受け付けない
    public static readonly BindableProperty BusyOverlayProperty = BindableProperty.CreateAttached(
        "BusyOverlay",
        typeof(bool),
        typeof(ShellProperty),
        false,
        propertyChanged: OnBusyOverlayChanged);

    public static bool GetBusyOverlay(BindableObject obj) =>
        (bool)obj.GetValue(BusyOverlayProperty);

    public static void SetBusyOverlay(BindableObject obj, bool value) =>
        obj.SetValue(BusyOverlayProperty, value);

    private static void OnBusyOverlayChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not Rectangle view)
        {
            return;
        }

        if (oldValue is true)
        {
            var behavior = view.Behaviors.FirstOrDefault(static x => x is BusyOverlayBehavior);
            if (behavior is not null)
            {
                view.Behaviors.Remove(behavior);
            }
        }

        if (newValue is true)
        {
            view.Behaviors.Add(new BusyOverlayBehavior());
        }
    }

    private sealed class BusyOverlayBehavior : BehaviorBase<Rectangle>
    {
        protected override void OnAttachedTo(Rectangle bindable)
        {
            base.OnAttachedTo(bindable);

            bindable.InputTransparent = false;
            bindable.BackgroundColor = Colors.Transparent;
            bindable.ZIndex = 1000;

            bindable.GestureRecognizers.Add(new TapGestureRecognizer());
        }
    }
}
