namespace TableOrder.Server.Web.Hubs;

using System.Threading.Channels;

// 通知をコミットした店舗を、配信の処理に知らせる (業務の処理は送り終わりを待たない)
public sealed class EventSignal : IEventPublisher
{
    private readonly Channel<(Guid TenantId, Guid StoreId)> channel = Channel.CreateUnbounded<(Guid TenantId, Guid StoreId)>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public ChannelReader<(Guid TenantId, Guid StoreId)> Reader => channel.Reader;

    public void Publish(Guid tenantId, Guid storeId) => channel.Writer.TryWrite((tenantId, storeId));
}
