namespace TableOrder.Server.Core.Accessors;

// 裏の処理 (通知の配信、古い通知の削除、開発の環境の自動の進行) がテナントをまたいで読み書きするもの
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

    // 残す期間を過ぎた通知を消す
    [Execute]
    public partial ValueTask<int> DeleteEventAsync(DateTimeOffset before, CancellationToken cancellationToken);

    // 期限を過ぎたペアリングコードを消す (コードはすべてのテナントで一意なので、消して出し直せるようにする)
    [Execute]
    public partial ValueTask<int> DeleteEnrollmentAsync(DateTimeOffset before, CancellationToken cancellationToken);
}
