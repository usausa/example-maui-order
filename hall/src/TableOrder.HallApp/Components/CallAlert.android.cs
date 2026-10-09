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
            ringtone?.Stop();
            using var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            ringtone = RingtoneManager.GetRingtone(context, uri);
            if (ringtone is not null)
            {
                // 通知の音量で鳴らす (既定は着信の音量)
                using var builder = new AudioAttributes.Builder();
                using var attributes = builder.SetUsage(AudioUsageKind.Notification)!.SetContentType(AudioContentType.Sonification)!.Build();
                ringtone.AudioAttributes = attributes;
                ringtone.Play();
            }

            // 振動はクラスで引く (名前で引く定数は Android 12 で廃止)
            if (context.GetSystemService(Java.Lang.Class.FromType(typeof(Vibrator))) is Vibrator { HasVibrator: true } vibrator)
            {
                using var effect = VibrationEffect.CreateWaveform(VibrationPattern, -1);
                vibrator.Vibrate(effect);
            }
        }
        catch (Java.Lang.Exception ex)
        {
            log.WarnCallAlertFailed(ex);
        }
    }
}
