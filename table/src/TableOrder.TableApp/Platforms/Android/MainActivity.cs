#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.TableApp;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

// テーブルに置くタブレットなので横向きに固定する (台に逆さに置いても使えるように 180 度の回転は許す)
// ロックタスクを許されていれば (Device Owner / EMM)、起動したときにシステムがロックタスクに入れる (落ちて起動し直したときも)
// 専用端末のホームアプリにもなる (Device Owner のときに KioskManager が常に使うホームにする)
// ホームの候補に入るだけでは、ホームの役割 (Android 10 以降) は替わらず、開発中の端末のホームはそのまま
[Activity(
    Name = "tableorder.terminal.table.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleTask,
    LockTaskMode = "if_whitelisted",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density |
                           ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Navigation | ConfigChanges.Touchscreen |
                           ConfigChanges.Locale | ConfigChanges.LayoutDirection | ConfigChanges.FontScale | ConfigChanges.Mcc | ConfigChanges.Mnc,
    ScreenOrientation = ScreenOrientation.SensorLandscape)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryHome, Intent.CategoryDefault])]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // アプリ全体のオーバースクロールを無効にする
        Theme?.ApplyStyle(_Microsoft.Android.Resource.Designer.ResourceConstant.Style.NoOverScrollTheme, true);

        base.OnCreate(savedInstanceState);
    }
}
