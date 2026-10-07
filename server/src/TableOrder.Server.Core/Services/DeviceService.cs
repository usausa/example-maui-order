namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Contract.Devices;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

public enum DevicePairStatus
{
    Success,
    CodeInvalid,
    TenantSuspended
}

public sealed record DevicePairResult(DevicePairStatus Status, DevicePairResponse? Device = null);

public enum DeviceAuthorizeStatus
{
    Success,
    Revoked,
    TenantSuspended
}

public sealed record DeviceAuthorizeResult(DeviceAuthorizeStatus Status, DeviceIdentity? Identity = null);

public sealed class DeviceService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly DirectoryAccessor directoryAccessor;

    private readonly TenantAccessor tenantAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly DeviceAccessor deviceAccessor;

    private readonly DeviceEnrollmentAccessor enrollmentAccessor;

    public DeviceService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        DirectoryAccessor directoryAccessor,
        TenantAccessor tenantAccessor,
        StoreAccessor storeAccessor,
        DeviceAccessor deviceAccessor,
        DeviceEnrollmentAccessor enrollmentAccessor)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.directoryAccessor = directoryAccessor;
        this.tenantAccessor = tenantAccessor;
        this.storeAccessor = storeAccessor;
        this.deviceAccessor = deviceAccessor;
        this.enrollmentAccessor = enrollmentAccessor;
    }

    //--------------------------------------------------------------------------------
    // Pair
    //--------------------------------------------------------------------------------

    // 端末の登録。テナントと店舗は、コードかトークンで引いた受け口の行で決める (要求の中の値は使わない)
    // 公開鍵は入口で形を確かめた JWK の JSON
    public async ValueTask<DevicePairResult> PairAsync(DevicePairRequest request, string publicKey, CancellationToken cancellationToken)
    {
        var now = contextProvider.Current.Now;
        var enrollment = !String.IsNullOrEmpty(request.PairingCode)
            ? await directoryAccessor.QueryEnrollmentByPairingCodeAsync(request.PairingCode, cancellationToken)
            : await directoryAccessor.QueryEnrollmentByTokenHashAsync(HashToken(request.EnrollmentToken ?? string.Empty), cancellationToken);
        if ((enrollment is null) || (enrollment.RevokedAt is not null) || (enrollment.ExpiresAt <= now) || (enrollment.UsedCount >= enrollment.MaxUses))
        {
            return new DevicePairResult(DevicePairStatus.CodeInvalid);
        }

        var tenant = await tenantAccessor.QueryAsync(enrollment.TenantId, cancellationToken);
        if (tenant?.Status != TenantStatus.Active)
        {
            return new DevicePairResult(DevicePairStatus.TenantSuspended);
        }

        var deviceId = Guid.CreateVersion7(now);
        var stationIds = String.IsNullOrEmpty(enrollment.StationIds) ? [] : JsonSerializer.Deserialize<List<Guid>>(enrollment.StationIds, JsonDefaults.Options) ?? [];
        var paired = false;
        await provider.UsingTxAsync(async (_, tx) =>
        {
            // 台数の確かめと数を足すのは 1 文で行う (同時の登録で台数を超えない)
            if (await enrollmentAccessor.AddUsedCountAsync(tx, enrollment.TenantId, enrollment.Id, now, cancellationToken) == 0)
            {
                return;
            }

            await deviceAccessor.InsertAsync(tx, enrollment.TenantId, deviceId, enrollment.StoreId, enrollment.Kind, request.DeviceName.Trim(), enrollment.TableId, publicKey, now, cancellationToken);
            foreach (var stationId in stationIds)
            {
                await deviceAccessor.InsertStationAsync(tx, enrollment.TenantId, deviceId, stationId, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            paired = true;
        }, cancellationToken);
        if (!paired)
        {
            return new DevicePairResult(DevicePairStatus.CodeInvalid);
        }

        await deviceAccessor.UpsertStatusAsync(enrollment.TenantId, deviceId, request.AppVersion, null, null, now, cancellationToken);

        return new DevicePairResult(DevicePairStatus.Success, new DevicePairResponse
        {
            DeviceId = deviceId,
            Kind = enrollment.Kind,
            StoreId = enrollment.StoreId
        });
    }

    // 登録トークンは平文で持たず、ハッシュで引く
    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    //--------------------------------------------------------------------------------
    // Token
    //--------------------------------------------------------------------------------

    // トークンの要求の署名を確かめるための端末 (テナントのわからないまま、すべてのテナントで一意の Id で引く)
    public ValueTask<DeviceEntity?> FindAsync(Guid deviceId, CancellationToken cancellationToken) =>
        directoryAccessor.QueryDeviceAsync(deviceId, cancellationToken);

    // 署名を確かめたあとに、トークンを出してよいかを決める。トークンに入れる値は端末の記録から作る
    public async ValueTask<DeviceAuthorizeResult> AuthorizeAsync(DeviceEntity device, CancellationToken cancellationToken)
    {
        if (!device.IsActive)
        {
            return new DeviceAuthorizeResult(DeviceAuthorizeStatus.Revoked);
        }

        var tenant = await tenantAccessor.QueryAsync(device.TenantId, cancellationToken);
        if (tenant?.Status != TenantStatus.Active)
        {
            return new DeviceAuthorizeResult(DeviceAuthorizeStatus.TenantSuspended);
        }

        var stations = device.Kind == DeviceKind.Kitchen
            ? (await deviceAccessor.QueryStationListAsync(device.TenantId, device.Id, cancellationToken)).Select(static x => x.StationId).ToList()
            : [];
        return new DeviceAuthorizeResult(
            DeviceAuthorizeStatus.Success,
            new DeviceIdentity(device.TenantId, device.StoreId, device.Id, device.Kind, device.TableId, stations));
    }

    //--------------------------------------------------------------------------------
    // Me
    //--------------------------------------------------------------------------------

    // 要求した端末の状態の報告
    public ValueTask<int> ReportStatusAsync(DeviceHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return deviceAccessor.UpsertStatusAsync(context.RequireTenantId(), context.RequireDeviceId(), request.AppVersion, request.BatteryLevel, request.IsCharging, context.Now, cancellationToken);
    }

    // 要求した端末の設定。置き場所はトークンではなく今の端末の記録から返す (席替えをすぐに出す)
    public async ValueTask<DeviceConfigResponse?> GetConfigAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var store = await storeAccessor.QueryAsync(tenantId, context.RequireStoreId(), cancellationToken);
        var device = await deviceAccessor.QueryAsync(tenantId, context.RequireDeviceId(), cancellationToken);
        if ((store is null) || (device is null))
        {
            return null;
        }

        var reasons = await storeAccessor.QueryCallReasonListAsync(tenantId, store.Id, cancellationToken);
        var table = device.TableId is { } tableId ? await storeAccessor.QueryTableAsync(tenantId, tableId, cancellationToken) : null;
        var stations = await deviceAccessor.QueryStationListAsync(tenantId, device.Id, cancellationToken);
        return new DeviceConfigResponse
        {
            StoreName = store.Name,
            Languages = JsonSerializer.Deserialize<List<string>>(store.Languages, JsonDefaults.Options) ?? [],
            OrderRules = new DeviceConfigResponseOrderRules
            {
                MaxQuantityPerLine = store.MaxQuantityPerLine,
                MaxLinesPerOrder = store.MaxLinesPerOrder,
                SelfStart = store.SelfStart
            },
            PaymentMethods = JsonSerializer.Deserialize<List<PaymentMethod>>(store.PaymentMethods, JsonDefaults.Options) ?? [],
            CallReasons = reasons.Select(static x => new DeviceConfigResponseCallReason
            {
                Code = x.Code,
                Name = x.Name,
                SortOrder = x.SortOrder
            }).ToList(),
            ElectronicReceipt = store.ElectronicReceipt,
            TaxRounding = store.TaxRounding,
            Device = new DeviceConfigResponseDevice
            {
                Id = device.Id,
                Kind = device.Kind,
                Name = device.Name,
                TableId = device.TableId,
                TableName = table?.Name,
                StationIds = stations.Select(static x => x.StationId).ToList()
            }
        };
    }
}
