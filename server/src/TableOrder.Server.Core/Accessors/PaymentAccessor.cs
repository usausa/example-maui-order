namespace TableOrder.Server.Core.Accessors;

// 支払と電子レシート
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class PaymentAccessor
{
    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, Guid storeId, Guid visitId, PaymentMethod method, decimal amount, string? qrCode, DateTimeOffset? expiresAt, string? provider, string? providerReference, Guid? deviceId, DateTimeOffset now, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<PaymentEntity?> QueryAsync(Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<PaymentEntity?> QueryAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid id, CancellationToken cancellationToken);

    // 来店の支払 (古い順)
    [Query]
    public partial ValueTask<List<PaymentEntity>> QueryListAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<PaymentEntity>> QueryListAsync(DbTransaction tx, Guid tenantId, Guid visitId, CancellationToken cancellationToken);

    // 店舗の待っている支払 (古い順)
    [Query]
    public partial ValueTask<List<PaymentEntity>> QueryPendingListAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken);

    // 払い終えた (Pending から)。決済サービスと取引番号は、送られたときだけ替える
    [Execute]
    public partial ValueTask<int> UpdateCompletedAsync(DbTransaction tx, Guid tenantId, Guid id, string? provider, string? providerReference, DateTimeOffset now, CancellationToken cancellationToken);

    // 払えなかった (Pending から)
    [Execute]
    public partial ValueTask<int> UpdateFailedAsync(DbTransaction tx, Guid tenantId, Guid id, string? provider, string? providerReference, string? reason, DateTimeOffset now, CancellationToken cancellationToken);

    // やめた (Pending から)
    [Execute]
    public partial ValueTask<int> UpdateCancelledAsync(DbTransaction tx, Guid tenantId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    // 電子レシートを作る (作ってあれば足さずに 0 件)
    [Execute]
    public partial ValueTask<int> InsertReceiptAsync(DbTransaction tx, Guid tenantId, Guid visitId, string token, DateTimeOffset now, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<ReceiptEntity?> QueryReceiptAsync(Guid tenantId, Guid visitId, CancellationToken cancellationToken);
}
