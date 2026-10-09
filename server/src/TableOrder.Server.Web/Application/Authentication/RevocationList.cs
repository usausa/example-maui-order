namespace TableOrder.Server.Web.Application.Authentication;

using TableOrder.Server.Web.Hubs;

// すぐに拒む一覧 (無効にした端末と止めたテナント)。アクセストークンを確かめたあとに引いて 401 にし、通知の接続も切る
// 要求ごとにデータベースを引かないように覚えておき、裏の処理が数秒ごとに読み直す (サーバを並べても、どのサーバも数秒で同じ一覧になる)
// 無効にする・止める操作を受けたサーバは、操作の中ですぐに読み直す
public sealed class RevocationList : IRevocationList, IDisposable
{
    private readonly TimeProvider timeProvider;

    private readonly StoreHubConnections connections;

    private readonly RevocationService revocationService;

    // 無効にしてから、この時間を過ぎた端末は持たない (トークンの期限と時計のずれを過ぎたので、トークンの確かめで断られる)
    private readonly TimeSpan revokedWindow;

    // 同時に読み直しても、先に読んだ古い一覧で後の一覧を上書きしない
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    private volatile RevocationResult current = RevocationResult.Empty;

    public RevocationList(
        TimeProvider timeProvider,
        StoreHubConnections connections,
        TokenSetting setting,
        RevocationService revocationService)
    {
        this.timeProvider = timeProvider;
        this.connections = connections;
        this.revocationService = revocationService;
        revokedWindow = TimeSpan.FromMinutes(setting.AccessTokenMinutes + 1);
    }

    public void Dispose() => refreshLock.Dispose();

    public bool IsRevoked(Guid tenantId, Guid deviceId) => current.Contains(tenantId, deviceId);

    // 読み直して、一覧に入った端末とテナントの通知の接続を切る
    public async ValueTask RefreshAsync(CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            current = await revocationService.GetAsync(timeProvider.GetUtcNow() - revokedWindow, cancellationToken);
        }
        finally
        {
            refreshLock.Release();
        }

        connections.Abort(IsRevoked);
    }
}
