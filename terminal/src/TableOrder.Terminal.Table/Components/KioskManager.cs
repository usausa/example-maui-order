namespace TableOrder.Terminal.Table.Components;

// 専用端末の方式
public enum KioskMode
{
    // 専用端末の仕組みがない (開発中の端末など)。全画面にするだけ
    None,

    // 外部の EMM (Android Enterprise の専用端末) がロックタスクを許している。端末の制限は EMM が掛ける
    Managed,

    // アプリ自身が Device Owner。端末の制限もアプリが掛ける
    DeviceOwner
}

// 専用端末の今の状態 (スタッフメニューに出す)
public sealed record KioskStatus(
    KioskMode Mode,
    bool IsLocked,
    bool IsReleased);

// 専用端末にする (全画面、ロックタスク、Device Owner のときは端末の制限)
// スタッフメニューから一時的に解除でき、アプリを起動し直すと専用端末に戻る
public sealed partial class KioskManager
{
    private readonly ILogger<KioskManager> log;

    private readonly IScreen screen;

    private KioskMode mode;

    // スタッフが一時的に解除している
    public bool IsReleased { get; private set; }

    // システムバーを隠しているか (ポップアップの窓も合わせる)
    public bool IsFullscreen => !IsReleased;

    public KioskManager(
        ILogger<KioskManager> log,
        IScreen screen)
    {
        this.log = log;
        this.screen = screen;
    }

    // 起動したときに呼ぶ (全画面とロックタスクは Resume で入る)
    public void Initialize()
    {
        mode = ResolveMode();
        ApplyMode();
    }

    // 画面が前に出たときに呼ぶ。ほかの窓から戻るとシステムバーが出ることがあるので、全画面をかけ直す
    // 動いている間に Device Owner になったり EMM が許したりすることがあるので、方式を読み直して変わっていれば掛け直す
    // ロックタスクは許されているときだけ入る (許されていないと画面の固定の確認が出るため)
    public void Resume()
    {
        screen.SetFullscreen(IsFullscreen);

        var current = ResolveMode();
        if (current != mode)
        {
            mode = current;
            ApplyMode();
        }

        if (!IsReleased && (mode != KioskMode.None) && !ResolveLocked())
        {
            StartLockTask();
        }
    }

    // スタッフが端末を触れるように、ロックタスクを抜けてシステムバーを出す
    public void Release()
    {
        IsReleased = true;
        if (ResolveLocked())
        {
            StopLockTask();
        }

        screen.SetFullscreen(false);
        log.InfoKioskReleased();
    }

    public void Restore()
    {
        IsReleased = false;
        Resume();
        log.InfoKioskRestored();
    }

    // 出すときは方式を読み直す (動いている間に EMM がロックタスクを許したり外したりする。掛け直しは画面が前に出たときに行う)
    public KioskStatus GetStatus() => new(ResolveMode(), ResolveLocked(), IsReleased);

    // Device Owner なら端末の制限を掛ける (EMM のときは EMM が掛ける)
    private void ApplyMode()
    {
        log.InfoKioskMode(mode);

        if (mode == KioskMode.DeviceOwner)
        {
            ApplyPolicies();
        }
    }

    // 解除している間だけ、Android の設定 (ネットワークなど) を開ける
    public void OpenSystemSettings()
    {
        if (IsReleased)
        {
            OpenSettings();
        }
    }

    private static partial KioskMode ResolveMode();

    private static partial bool ResolveLocked();

    private static partial void ApplyPolicies();

    private static partial void StartLockTask();

    private static partial void StopLockTask();

    private static partial void OpenSettings();
}
