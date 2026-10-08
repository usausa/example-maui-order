namespace TableOrder.Server.Core.Accessors;

// テナントのわからない要求 (端末の登録、トークンの要求) で、テナントを決めるための行を引く
// すべてのテナントで一意の列で引き、引いた行のテナントをその後の処理の文脈にする (テナントの条件を調べるテストから外す)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DirectoryAccessor
{
    [QueryFirst]
    public partial ValueTask<DeviceEntity?> QueryDeviceAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<DeviceEnrollmentEntity?> QueryEnrollmentByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<DeviceEnrollmentEntity?> QueryEnrollmentByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    // 決済サービスの結果の通知で、テナントのわからないまま取引番号で支払を引く
    [QueryFirst]
    public partial ValueTask<PaymentEntity?> QueryPaymentByProviderReferenceAsync(string provider, string providerReference, CancellationToken cancellationToken);
}
