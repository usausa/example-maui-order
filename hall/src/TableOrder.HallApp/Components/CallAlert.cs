namespace TableOrder.HallApp.Components;

// 新しい呼び出しを、端末の通知の音と振動で知らせる (音量とマナーモードは端末の設定に従う)
// 続けて届いたとき (つなぎ直して抜けた通知をまとめて受けたときなど) は、間を空けて 1 回にまとめる
public sealed partial class CallAlert
{
    // 続けて知らせない間
    private static readonly TimeSpan QuietInterval = TimeSpan.FromSeconds(3);

    private readonly ILogger<CallAlert> log;

    private readonly TimeProvider timeProvider;

    private DateTimeOffset lastAlerted = DateTimeOffset.MinValue;

    public CallAlert(
        ILogger<CallAlert> log,
        TimeProvider timeProvider)
    {
        this.log = log;
        this.timeProvider = timeProvider;
    }

    public void Alert()
    {
        var now = timeProvider.GetUtcNow();
        if (now - lastAlerted < QuietInterval)
        {
            return;
        }

        lastAlerted = now;
        Play();
    }

    private partial void Play();
}
