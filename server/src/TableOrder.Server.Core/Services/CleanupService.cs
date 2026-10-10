namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// 店舗の古いデータの片付け (残す期間を過ぎた閉じた来店とその子の表、公開し直したメニュー)
// 裏の処理 (CleanupWorker) が店舗ごとに文脈を始めて呼ぶ。今の状態は変わらないので通知は書かない
public sealed class CleanupService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly BackgroundAccessor backgroundAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly CleanupAccessor cleanupAccessor;

    public CleanupService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        BackgroundAccessor backgroundAccessor,
        StoreAccessor storeAccessor,
        CleanupAccessor cleanupAccessor)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.backgroundAccessor = backgroundAccessor;
        this.storeAccessor = storeAccessor;
        this.cleanupAccessor = cleanupAccessor;
    }

    // 片付ける店舗 (テナントをまたぐ)
    public ValueTask<List<StoreKeyEntity>> GetStoreAllAsync(CancellationToken cancellationToken) =>
        backgroundAccessor.QueryStoreAllAsync(cancellationToken);

    // 文脈の店舗の、営業日から retentionDays 日を過ぎた閉じた来店 (払い終えた、取りやめた) を、子の表から消す。消した来店の数を返す
    // batchSize 件ずつのトランザクションに分け、ほかの書き込みを長く止めない
    public async ValueTask<int> DeleteClosedVisitsAsync(int retentionDays, int batchSize, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var store = await storeAccessor.QueryAsync(tenantId, storeId, cancellationToken);
        if (store is null)
        {
            return 0;
        }

        var before = StoreHours.BusinessDate(context.Now, store.TimeZone, StoreHours.Parse(store.OpenTime)).AddDays(-retentionDays);
        var total = 0;
        while (true)
        {
            var deleted = await provider.UsingTxAsync(async (_, tx) =>
            {
                // 外部キーの子から消す (明細はチケットを、チケットは注文を指す)
                await cleanupAccessor.DeleteOrderLineOptionAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteOrderLineAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteKitchenTicketAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteOrderAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteVisitConfirmationAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteCallAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeletePaymentAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await cleanupAccessor.DeleteReceiptAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                var visits = await cleanupAccessor.DeleteVisitAsync(tx, tenantId, storeId, before, batchSize, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return visits;
            }, cancellationToken);

            total += deleted;
            if (deleted < batchSize)
            {
                return total;
            }
        }
    }

    // 文脈の店舗の、今のメニューと公開の新しい順に kept 件を残して、公開し直したメニューを消す。消した数を返す
    public ValueTask<int> DeleteMenuPublicationsAsync(int kept, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return cleanupAccessor.DeleteMenuPublicationAsync(context.RequireTenantId(), context.RequireStoreId(), kept, cancellationToken);
    }
}
