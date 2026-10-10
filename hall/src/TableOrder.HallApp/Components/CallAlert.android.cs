namespace TableOrder.HallApp.Components;

using Android.App;
using Android.Media;
using Android.OS;

public sealed partial class CallAlert
{
    // 振動の形 (待ち、振動、間、振動のミリ秒。短く 2 回)
    private static readonly long[] VibrationPattern = [0, 300, 150, 300];

    // 鳴らしている音 (鳴り終わる前に回収されて止まらないように持っておく)
    private Ringtone? ringtone;

    // 知らせられなくても (通知の音がないなど) 通知の扱いは続ける
    private partial void Play()
    {
        var context = Application.Context;
        try
        {
            // 通知の音量で鳴らす (既定は着信の音量)
            using var builder = new AudioAttributes.Builder();
            using var attributes = builder.SetUsage(AudioUsageKind.Notification)!.SetContentType(AudioContentType.Sonification)!.Build()!;

            ringtone?.Stop();
            using var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            ringtone = RingtoneManager.GetRingtone(context, uri);
            if (ringtone is not null)
            {
                ringtone.AudioAttributes = attributes;
                ringtone.Play();
            }

            // 振動はクラスで引く (名前で引く定数は Android 12 で廃止)
            // 通知の用途を付ける (端末の通知の振動の設定に従う。用途のない振動は、裏にいる間は捨てられることがある)
            if (context.GetSystemService(Java.Lang.Class.FromType(typeof(Vibrator))) is Vibrator { HasVibrator: true } vibrator)
            {
                using var effect = VibrationEffect.CreateWaveform(VibrationPattern, -1)!;
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    using var usage = VibrationAttributes.CreateForUsage((int)VibrationAttributesUsageType.Notification);
                    vibrator.Vibrate(effect, usage);
                }
                else
                {
                    vibrator.Vibrate(effect, attributes);
                }
            }
        }
        catch (Java.Lang.Exception ex)
        {
            log.WarnCallAlertFailed(ex);
        }
    }
}
