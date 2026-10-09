namespace TableOrder.Server.Core.Services;

using System.Collections.Frozen;

using TableOrder.Server.Core.Accessors;

// すぐに拒む一覧の中身 (無効にした端末と、使えないテナント)
public sealed record RevocationResult(FrozenSet<Guid> DeviceIds, FrozenSet<Guid> TenantIds)
{
    public static RevocationResult Empty { get; } = new([], []);

    public bool Contains(Guid tenantId, Guid deviceId) => TenantIds.Contains(tenantId) || DeviceIds.Contains(deviceId);
}

// すぐに拒む一覧を読む (裏の処理と、無効にする・止める操作のあとに呼ぶ。テナントをまたいで読む)
public sealed class RevocationService
{
    private readonly BackgroundAccessor backgroundAccessor;

    public RevocationService(BackgroundAccessor backgroundAccessor)
    {
        this.backgroundAccessor = backgroundAccessor;
    }

    // revokedSince より後に無効にした端末 (それより前に無効にした端末のトークンは期限を過ぎている) と、止めた・解約したテナント
    public async ValueTask<RevocationResult> GetAsync(DateTimeOffset revokedSince, CancellationToken cancellationToken)
    {
        var devices = await backgroundAccessor.QueryRevokedDeviceAllAsync(revokedSince, cancellationToken);
        var tenants = await backgroundAccessor.QueryInactiveTenantAllAsync(cancellationToken);
        return new RevocationResult(devices.Select(static x => x.Id).ToFrozenSet(), tenants.Select(static x => x.Id).ToFrozenSet());
    }
}
