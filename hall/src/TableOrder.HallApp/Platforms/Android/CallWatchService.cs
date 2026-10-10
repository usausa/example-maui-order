#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.HallApp;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

using ResourceConstant = _Microsoft.Android.Resource.Designer.ResourceConstant;

// 画面が消えている間も新しい呼び出しを知らせるための前景サービス (CallWatch が始める。呼び出しの音と振動は CallAlert が鳴らす)
// 動いている間はアプリが前景の扱いになり、Doze でも通信できて通知の接続を保てる
// 部分ウェイクロックで CPU を止めない (止まると、通知の接続の生存確認が遅れて切れ、届いた呼び出しの音も遅れる)
// 種類の用途を書く property を付けるので、マニフェストに書く (Java の名前を固定する)
[Register("tableorder/terminal/hall/CallWatchService")]
public sealed class CallWatchService : Service
{
    private const int NotificationId = 1;

    private const string ChannelId = "call_watch";

    private const string WakeLockTag = "TableOrderHall:CallWatch";

    private PowerManager.WakeLock? wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // 始めるたびに前景にする (始めてから決まった時間のうちに前景にしないと、システムが止める)
        using var notification = CreateNotification();
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        if ((wakeLock is null) && (GetSystemService(Java.Lang.Class.FromType(typeof(PowerManager))) is PowerManager power))
        {
            wakeLock = power.NewWakeLock(WakeLockFlags.Partial, WakeLockTag);
            wakeLock?.SetReferenceCounted(false);
            wakeLock?.Acquire();
        }

        // プロセスが止められたら、次に画面が前に出たときに始め直す (裏からは前景サービスを始められない)
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (wakeLock is { IsHeld: true })
        {
            wakeLock.Release();
        }

        base.OnDestroy();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            wakeLock?.Dispose();
            wakeLock = null;
        }

        base.Dispose(disposing);
    }

    // 音のない知らせのチャネルに出し、押すとアプリの画面に戻る (知らせは通知の許可がある端末で出る。なくても前景サービスは動く)
    private Notification CreateNotification()
    {
        if (GetSystemService(Java.Lang.Class.FromType(typeof(NotificationManager))) is NotificationManager manager)
        {
            using var channel = new NotificationChannel(ChannelId, AppResources.CallWatchChannel, NotificationImportance.Low);
            manager.CreateNotificationChannel(channel);
        }

        using var launch = new Intent(this, typeof(MainActivity));
        using var pending = PendingIntent.GetActivity(this, 0, launch, PendingIntentFlags.Immutable);
        using var builder = new Notification.Builder(this, ChannelId);
        return builder
            .SetSmallIcon(ResourceConstant.Drawable.ic_call_watch)
            .SetContentTitle(AppResources.CallWatchTitle)
            .SetContentText(AppResources.CallWatchText)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
    }
}
