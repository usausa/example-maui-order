namespace TableOrder.Server.Web.Hubs;

using System.Collections.Concurrent;

using Microsoft.AspNetCore.SignalR;

// つないでいる端末の接続。すぐに拒む一覧に入った端末とテナントの接続を切るために覚える
// (WebSocket はつないだときにだけトークンを確かめるので、切らないと無効にした端末に通知が届き続ける)
public sealed class StoreHubConnections
{
    private readonly ConcurrentDictionary<string, Connection> connections = new();

    public void Add(HubCallerContext context, Guid tenantId, Guid deviceId) =>
        connections[context.ConnectionId] = new Connection(context, tenantId, deviceId);

    public void Remove(string connectionId) => connections.TryRemove(connectionId, out _);

    // 当たる接続を切る (端末はつなぎ直しを断られ、トークンの要求で無効と停止を知る)
    public void Abort(Func<Guid, Guid, bool> isRevoked)
    {
        foreach (var connection in connections.Values)
        {
            if (isRevoked(connection.TenantId, connection.DeviceId))
            {
                connection.Context.Abort();
            }
        }
    }

    private sealed record Connection(HubCallerContext Context, Guid TenantId, Guid DeviceId);
}
