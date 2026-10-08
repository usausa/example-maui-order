#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.HallApp;

using Android.App;
using Android.App.Admin;
using Android.Content;

// Device Owner (アプリ自身で専用端末にする方式) の受け口。端末の制限は KioskManager が掛ける
// adb から Device Owner にするときの名前になるので、Java の名前を固定する
[BroadcastReceiver(
    Name = "tableorder.terminal.hall.AdminReceiver",
    Permission = "android.permission.BIND_DEVICE_ADMIN",
    Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/device_admin")]
[IntentFilter([DeviceAdminReceiver.ActionDeviceAdminEnabled])]
public sealed class AdminReceiver : DeviceAdminReceiver;
