namespace TableOrder.Server.Core.Accessors;

// 店舗の古いデータの片付け (残す期間を過ぎた閉じた来店とその子の表、公開し直したメニュー)
// 来店は営業日の古い順に limit 件を選び、同じトランザクションの中で子の表から順に消す (どの文も同じ来店を選ぶ)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class CleanupAccessor
{
    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    [Execute]
    public partial ValueTask<int> DeleteOrderLineOptionAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteOrderLineAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteKitchenTicketAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteOrderAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteVisitConfirmationAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteCallAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeletePaymentAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteReceiptAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    // 来店は最後に消す (消した来店の数を返す)
    [Execute]
    public partial ValueTask<int> DeleteVisitAsync(DbTransaction tx, Guid tenantId, Guid storeId, DateOnly before, int limit, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Menu
    //--------------------------------------------------------------------------------

    // 今のメニューと、公開の新しい順に kept 件を残して消す
    [Execute]
    public partial ValueTask<int> DeleteMenuPublicationAsync(Guid tenantId, Guid storeId, int kept, CancellationToken cancellationToken);
}
