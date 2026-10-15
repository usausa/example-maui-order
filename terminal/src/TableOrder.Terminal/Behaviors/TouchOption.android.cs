namespace TableOrder.Terminal.Behaviors;

using System.Runtime.CompilerServices;

using Android.Content.Res;
using Android.Graphics.Drawables;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

public static partial class TouchOption
{
    // 波紋の色の役割と濃さ
    private const string RippleColorKey = "OnSurfaceColor";

    private const float RippleAlpha = 0.16f;

    private static readonly ConditionalWeakTable<Android.Views.View, ClickListener> Listeners = [];

    public static partial void UseCustomMapper(BehaviorOptions options)
    {
        ViewHandler.ViewMapper.AppendToMapping(RippleProperty.PropertyName, UpdateRipple);
        ViewHandler.ViewMapper.AppendToMapping(ClickCommandProperty.PropertyName, UpdateClickCommand);
    }

    // 付けたビューだけを変える (ほかのビューの前景には触れない)
    // 色はビューを作るときの資源から引く (チェーンの色は起動で入れ、画面を作り直して反映する)
    private static void UpdateRipple(IViewHandler handler, IView view)
    {
        if ((handler.PlatformView is not Android.Views.View platformView) ||
            (view is not BindableObject bindable) ||
            !bindable.IsSet(RippleProperty))
        {
            return;
        }

        if (GetRipple(bindable) && (Application.Current?.Resources.FindResource<Color>(RippleColorKey) is { } color))
        {
            // 波紋は付けたビューの四角の中だけに描く (マスクは形だけを使い、色は出ない)
            using var colors = ColorStateList.ValueOf(color.WithAlpha(RippleAlpha).ToPlatform());
            using var mask = new ColorDrawable(Android.Graphics.Color.White);
            platformView.Foreground = new RippleDrawable(colors, null, mask);
            platformView.Clickable = true;
        }
        else
        {
            platformView.Foreground = null;
        }
    }

    private static void UpdateClickCommand(IViewHandler handler, IView view)
    {
        if ((handler.PlatformView is Android.Views.View platformView) &&
            (view is BindableObject bindable) &&
            bindable.IsSet(ClickCommandProperty))
        {
            platformView.SetOnClickListener(GetClickCommand(bindable) is not null ? Listeners.GetValue(platformView, _ => new ClickListener(bindable)) : null);
        }
    }

    // ビューを持ち続けないように、付けた要素は弱い参照で持つ
    private sealed class ClickListener : Java.Lang.Object, Android.Views.View.IOnClickListener
    {
        private readonly WeakReference<BindableObject> target;

        public ClickListener(BindableObject bindable)
        {
            target = new WeakReference<BindableObject>(bindable);
        }

        public void OnClick(Android.Views.View? v)
        {
            if (target.TryGetTarget(out var bindable) && (GetClickCommand(bindable) is { } command))
            {
                var parameter = GetClickCommandParameter(bindable);
                if (command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                }
            }
        }
    }
}
