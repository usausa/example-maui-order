namespace TableOrder.Server.Core.Accessors;

// 裏の処理 (通知の配信、古い通知の削除、すぐに拒む一覧、開発の環境の自動の進行、片付ける店舗の一覧) がテナントをまたいで読み書きするもの
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class BackgroundAccessor
{
    // すべての店舗の通知の通し番号
    [Query]
    public partial ValueTask<List<EventSequenceEntity>> QueryEventSequenceAllAsync(CancellationToken cancellationToken);

    // 進めるもの (提供の前の明細、終わっていない呼び出し、待っている支払) のある店舗 (開発の環境で時間で進める)
    [Query]
    public partial ValueTask<List<StoreKeyEntity>> QuerySimulationStoreAllAsync(CancellationToken cancellationToken);

    // すべての店舗 (使わなくした店舗も。古いデータを店舗ごとに片付ける)
    [Query]
    public partial ValueTask<List<StoreKeyEntity>> QueryStoreAllAsync(CancellationToken cancellationToken);

    // since より後に無効にした端末 (すぐに拒む一覧)
    [Query]
    public partial ValueTask<List<DeviceKeyEntity>> QueryRevokedDeviceAllAsync(DateTimeOffset since, CancellationToken cancellationToken);

    // 使えないテナント (止めた、解約した。すぐに拒む一覧)
    [Query]
    public partial ValueTask<List<TenantEntity>> QueryInactiveTenantAllAsync(CancellationToken cancellationToken);

    // 残す期間を過ぎた通知を消す
    [Execute]
    public partial ValueTask<int> DeleteEventAsync(DateTimeOffset before, CancellationToken cancellationToken);

    // 期限を過ぎたペアリングコードを消す (コードはすべてのテナントで一意なので、消して出し直せるようにする)
    [Execute]
    public partial ValueTask<int> DeletePairingCodeAsync(DateTimeOffset before, CancellationToken cancellationToken);

    // 期限を過ぎるか取り消してから、before より前になった登録トークンを消す
    [Execute]
    public partial ValueTask<int> DeleteEnrollmentTokenAsync(DateTimeOffset before, CancellationToken cancellationToken);
}
