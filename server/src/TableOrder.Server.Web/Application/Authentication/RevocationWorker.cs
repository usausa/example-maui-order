namespace TableOrder.Server.Web.Application.Authentication;

// すぐに拒む一覧を、起動のときと一定の間隔 (Token:RevocationSweepSeconds) で読み直す (ほかのサーバで無効にした端末と止めたテナントも断る)
public sealed class RevocationWorker : BackgroundService
{
    private readonly ILogger<RevocationWorker> log;

    private readonly TimeProvider timeProvider;

    private readonly TokenSetting setting;

    private readonly RevocationList revocationList;

    public RevocationWorker(
        ILogger<RevocationWorker> log,
        TimeProvider timeProvider,
        TokenSetting setting,
        RevocationList revocationList)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.setting = setting;
        this.revocationList = revocationList;
    }

    // 要求を受け始める前に読む (起動の前に止めたテナントの端末を通さない)
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await revocationList.RefreshAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(setting.RevocationSweepSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await revocationList.RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // 次の間隔で読み直す (それまでは前の一覧で断る)
                log.ErrorRevocationRefresh(ex);
            }
        }
    }
}
