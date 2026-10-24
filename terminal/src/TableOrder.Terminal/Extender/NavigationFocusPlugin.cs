namespace TableOrder.Terminal.Extender;

using Smart.Navigation.Plugins;

// 前の画面で開いたソフトキーボードは、画面を移っても Android が閉じないので、移った後に閉じる
public sealed partial class NavigationFocusPlugin : PluginBase
{
    public override void OnNavigatedTo(IPluginContext pluginContext, INavigationContext navigationContext, object view, object? target)
    {
        HideSoftInput();
    }

    private static partial void HideSoftInput();
}
