#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.Terminal.Hall;

using Android.App;
using Android.Content.PM;
using Android.OS;

using AndroidX.Activity;

// スタッフが手に持つスマートフォンなので縦向きに固定する (逆さに持っても使えるように 180 度の回転は許す)
[Activity(
    Name = "tableorder.terminal.hall.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.SensorPortrait)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private BackPressedCallback? backPressedCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        backPressedCallback = new BackPressedCallback(this);
        OnBackPressedDispatcher.AddCallback(this, backPressedCallback);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            backPressedCallback?.Dispose();
            backPressedCallback = null;
        }

        base.Dispose(disposing);
    }

    // 戻るは MainPage が画面へ渡す。MainPage が扱わないときだけアプリを終了せずタスクを背面へ回す
    private sealed class BackPressedCallback : OnBackPressedCallback
    {
        private readonly MainActivity activity;

        public BackPressedCallback(MainActivity activity)
            : base(true)
        {
            this.activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
            var page = windows is { Count: > 0 } ? windows[^1].Page : null;
            if (page?.SendBackButtonPressed() ?? false)
            {
                return;
            }

            activity.MoveTaskToBack(true);
        }
    }
}
