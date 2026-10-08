namespace TableOrder.Terminal.Extender;

using TableOrder.Terminal.Components;

// 全画面の間は、ポップアップの窓でもシステムバーを隠す
// ポップアップは Activity と別の窓 (ダイアログ) に出るので、Activity の全画面の設定が効かずにシステムバーが出てしまう
public sealed class FullscreenPopupPlugin : IPopupPlugin
{
    private readonly KioskManager kiosk;

    public FullscreenPopupPlugin(KioskManager kiosk)
    {
        this.kiosk = kiosk;
    }

    public void Extend(ContentView view)
    {
        // スタッフが専用端末を解除している間は、システムバーを出したままにする
        if (kiosk.IsFullscreen)
        {
            view.Loaded += OnLoaded;
        }
    }

    private static void OnLoaded(object? sender, EventArgs e)
    {
        if (sender is not ContentView view)
        {
            return;
        }

        view.Loaded -= OnLoaded;

#if ANDROID
        // ビューが載っている窓 (ダイアログ) のシステムバーを隠す
        if ((view.Handler?.PlatformView is Android.Views.View platformView) && (platformView.WindowInsetsController is { } controller))
        {
            controller.Hide(Android.Views.WindowInsets.Type.SystemBars());
            controller.SystemBarsBehavior = (int)Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
#endif
    }
}
