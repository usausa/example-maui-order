namespace TableOrder.Server.Web.Application.Simulation;

using TableOrder.Server.Web.Application.Context;

// 開発の環境の自動の進行。進めるもののある店舗ごとに、店舗の文脈を始めて進める
// 動かすかは起動したあとに設定で決める (テストのサーバが起動の途中で設定を替えても効くように)
public sealed class SimulationWorker : BackgroundService
{
    private readonly ILogger<SimulationWorker> log;

    private readonly TimeProvider timeProvider;

    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly SimulationSetting setting;

    private readonly SimulationService simulationService;

    private readonly SimulationTiming timing;

    public SimulationWorker(
        ILogger<SimulationWorker> log,
        TimeProvider timeProvider,
        ApplicationServiceContextProvider contextProvider,
        SimulationSetting setting,
        SimulationService simulationService)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.contextProvider = contextProvider;
        this.setting = setting;
        this.simulationService = simulationService;
        timing = new SimulationTiming(
            TimeSpan.FromSeconds(setting.CookingSeconds),
            TimeSpan.FromSeconds(setting.ReadySeconds),
            TimeSpan.FromSeconds(setting.ServedSeconds),
            TimeSpan.FromSeconds(setting.AcknowledgeSeconds),
            TimeSpan.FromSeconds(setting.CallDoneSeconds),
            TimeSpan.FromSeconds(setting.PaymentSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!setting.Enabled)
        {
            return;
        }

        log.InfoSimulationStart();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(setting.IntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            List<StoreKeyEntity> stores;
            try
            {
                stores = await simulationService.GetStoreAllAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.ErrorSimulation(ex);
                continue;
            }

            foreach (var store in stores)
            {
                await AdvanceAsync(store, stoppingToken);
            }
        }
    }

    // 1 つの店舗で失敗しても、ほかの店舗は進める (進められなかったものは次の間隔で進める)
    private async ValueTask AdvanceAsync(StoreKeyEntity store, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = contextProvider.Begin(() => new ServiceContext(timeProvider.GetUtcNow())
            {
                TenantId = store.TenantId,
                StoreId = store.StoreId
            });
            await simulationService.AdvanceAsync(timing, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            log.ErrorStoreSimulation(ex, store.TenantId, store.StoreId);
        }
    }
}
