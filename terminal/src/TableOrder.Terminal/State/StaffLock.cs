namespace TableOrder.Terminal.State;

public enum StaffPinResult
{
    Accepted,
    Rejected,
    Locked
}

// スタッフメニュー (と起動に失敗したときの端末の設定) に入る PIN の確かめ
// 店舗の設定のハッシュ (登録と一緒に保存したもの) と同じ計算で確かめ、間違いが続いたらしばらく入れない (アプリを起動し直すと数え直す)
// ハッシュを受け取る前 (登録していない端末) は PIN なしで入れる (接続先を直せるように)
public sealed class StaffLock
{
    private const int MaxFailures = 5;

    private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(5);

    private readonly TimeProvider timeProvider;

    private readonly Settings settings;

    private int failures;

    private DateTimeOffset lockedUntil;

    public StaffLock(
        TimeProvider timeProvider,
        Settings settings)
    {
        this.timeProvider = timeProvider;
        this.settings = settings;
    }

    public bool RequiresPin => settings.StaffPin is not null;

    public bool IsLocked => timeProvider.GetUtcNow() < lockedUntil;

    public async Task<StaffPinResult> VerifyAsync(string pin)
    {
        if (IsLocked)
        {
            return StaffPinResult.Locked;
        }

        if (settings.StaffPin is not { } hash)
        {
            return StaffPinResult.Accepted;
        }

        // 回数の多い計算なので、画面のスレッドの外で行う
        if (await Task.Run(() => StaffPins.Verify(pin, hash.Iterations, hash.Salt, hash.Hash)))
        {
            failures = 0;
            return StaffPinResult.Accepted;
        }

        failures++;
        if (failures < MaxFailures)
        {
            return StaffPinResult.Rejected;
        }

        failures = 0;
        lockedUntil = timeProvider.GetUtcNow() + LockTime;
        return StaffPinResult.Locked;
    }
}
