namespace TableOrder.Terminal.Extender;

using Smart.Maui;
using Smart.Navigation.Plugins;

// 画面を移ったら、前の画面で押したボタンの押した見た目 (Android の波紋) を止める (新しい画面に残らないように)
public sealed class NavigationFeedbackPlugin : PluginBase
{
    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
#if ANDROID
        if (view is not Element element)
        {
            return;
        }

        var page = element.FindParent<Page>();
        if (page is null)
        {
            return;
        }

        Application.Current?.Dispatcher.Dispatch(() =>
        {
            // ViewGroup の JumpDrawablesToCurrentState は子孫にも伝わる
            if (page.Handler?.PlatformView is Android.Views.View platformView)
            {
                platformView.JumpDrawablesToCurrentState();
            }
        });
#endif
    }
}
