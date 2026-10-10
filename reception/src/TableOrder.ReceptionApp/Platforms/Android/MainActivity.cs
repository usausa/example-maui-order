#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.ReceptionApp;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

using AndroidX.Activity;

// 店の入口に縦に置くタブレットなので縦向きに固定する (台に逆さに置いても使えるように 180 度の回転は許す)
// ロックタスクを許されていれば (Device Owner / EMM)、起動したときにシステムがロックタスクに入れる (落ちて起動し直したときも)
// 専用端末のホームアプリにもなる (Device Owner のときに KioskManager が常に使うホームにする)
// ホームの候補に入るだけでは、ホームの役割 (Android 10 以降) は替わらず、開発中の端末のホームはそのまま
[Activity(
    Name = "tableorder.terminal.reception.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    LockTaskMode = "if_whitelisted",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
    ScreenOrientation = ScreenOrientation.SensorPortrait)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryHome, Intent.CategoryDefault])]
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
