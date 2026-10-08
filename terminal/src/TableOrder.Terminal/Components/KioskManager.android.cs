namespace TableOrder.Terminal.Components;

using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.OS;

using AndroidSettings = Android.Provider.Settings;

public sealed partial class KioskManager
{
    // OS の更新を入れる時間帯 (夜間の 2:00 から 4:00。0 時からの分)
    private const int UpdateWindowStart = 2 * 60;

    private const int UpdateWindowEnd = 4 * 60;

    // Device Owner のときに禁止する操作 (工場出荷時へのリセット、安全モード、利用者の追加)
    private static readonly string[] Restrictions =
    [
        UserManager.DisallowFactoryReset,
        UserManager.DisallowSafeBoot,
        UserManager.DisallowAddUser
    ];

    private static Context Context => Application.Context;

    private static DevicePolicyManager PolicyManager => (DevicePolicyManager)Context.GetSystemService(Context.DevicePolicyService)!;

    private static partial KioskMode ResolveMode()
    {
        var package = Context.PackageName;
        var policy = PolicyManager;
        if (policy.IsDeviceOwnerApp(package))
        {
            return KioskMode.DeviceOwner;
        }

        return policy.IsLockTaskPermitted(package) ? KioskMode.Managed : KioskMode.None;
    }

    private static partial bool ResolveLocked()
    {
        var manager = (ActivityManager)Context.GetSystemService(Context.ActivityService)!;
        return manager.LockTaskModeState != LockTaskMode.None;
    }

    private static partial void ApplyPolicies(KioskOptions options)
    {
        var policy = PolicyManager;
        using var admin = new ComponentName(Context, Java.Lang.Class.FromType(options.AdminReceiver));
        using var home = new ComponentName(Context, Java.Lang.Class.FromType(options.HomeActivity));

        // ロックタスクに入れるのはこのアプリだけにし、ホーム・履歴・通知・電源のメニューを出さない
        policy.SetLockTaskPackages(admin, [Context.PackageName!]);
        policy.SetLockTaskFeatures(admin, LockTaskFeatures.None);

        // 常に使うホームアプリにして、電源を入れたときと落ちたときにこのアプリが起動するようにする
        using var filter = new IntentFilter(Intent.ActionMain);
        filter.AddCategory(Intent.CategoryHome);
        filter.AddCategory(Intent.CategoryDefault);
        policy.AddPersistentPreferredActivity(admin, filter, home);

        // ロック画面とステータスバーを止める
        policy.SetKeyguardDisabled(admin, true);
        policy.SetStatusBarDisabled(admin, true);

        // 充電中は画面を点けたままにする (AC、USB、ワイヤレスのどれでも)
        var plugged = (int)BatteryPlugged.Ac | (int)BatteryPlugged.Usb | (int)BatteryPlugged.Wireless;
        policy.SetGlobalSetting(admin, AndroidSettings.Global.StayOnWhilePluggedIn, plugged.ToString(CultureInfo.InvariantCulture));

        // OS の更新は夜間に入れる
        using var update = SystemUpdatePolicy.CreateWindowedInstallPolicy(UpdateWindowStart, UpdateWindowEnd);
        policy.SetSystemUpdatePolicy(admin, update);

        foreach (var restriction in Restrictions)
        {
            policy.AddUserRestriction(admin, restriction);
        }
    }

    private static partial void StartLockTask() => Platform.CurrentActivity?.StartLockTask();

    private static partial void StopLockTask() => Platform.CurrentActivity?.StopLockTask();

    private static partial void OpenSettings()
    {
        if (Platform.CurrentActivity is not { } activity)
        {
            return;
        }

        using var intent = new Intent(AndroidSettings.ActionSettings);
        activity.StartActivity(intent);
    }
}
