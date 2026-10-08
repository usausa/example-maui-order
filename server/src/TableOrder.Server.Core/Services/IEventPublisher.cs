namespace TableOrder.Server.Core.Services;

// コミットした通知を端末に送る。送るのは入口の側 (ハブ) で、業務の処理は通知が増えた店舗だけを知らせる
public interface IEventPublisher
{
    void Publish(Guid tenantId, Guid storeId);
}
