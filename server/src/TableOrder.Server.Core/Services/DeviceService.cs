namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Contract.Devices;
using TableOrder.Contract.Events;
using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

public enum DeviceAuthorizeStatus
{
    Success,
    Revoked,
    TenantSuspended
}

public sealed record DeviceAuthorizeResult(DeviceAuthorizeStatus Status, DeviceIdentity? Identity = null);

// 管理画面の端末の一覧の行 (端末と、キッチン端末の持ち場)
public sealed record DeviceSummaryResult(DeviceSummaryEntity Device, IReadOnlyList<Guid> StationIds)
{
    // 置き場所の割り当てを待つ (登録トークンで登録した端末は、テーブルと持ち場を決めずに登録して、起動の画面で待つ)
    public bool IsWaitingPlacement =>
        Device.IsActive && Device.Kind switch
        {
            DeviceKind.Table => Device.TableId is null,
            DeviceKind.Kitchen => StationIds.Count == 0,
            _ => false
        };
}

public sealed class DeviceService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IRevocationList revocationList;

    private readonly DirectoryAccessor directoryAccessor;

    private readonly TenantAccessor tenantAccessor;

    private readonly StoreAccessor storeAccessor;

    private readonly SettingsAccessor settingsAccessor;

    private readonly DeviceAccessor deviceAccessor;

    private readonly DeviceEnrollmentAccessor enrollmentAccessor;

    private readonly MenuService menuService;

    private readonly EventService eventService;

    public DeviceService(
        ServiceContextProvider contextProvider,
        IRevocationList revocationList,
        DirectoryAccessor directoryAccessor,
        TenantAccessor tenantAccessor,
        StoreAccessor storeAccessor,
        SettingsAccessor settingsAccessor,
        DeviceAccessor deviceAccessor,
        DeviceEnrollmentAccessor enrollmentAccessor,
        MenuService menuService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.revocationList = revocationList;
        this.directoryAccessor = directoryAccessor;
        this.tenantAccessor = tenantAccessor;
        this.storeAccessor = storeAccessor;
        this.settingsAccessor = settingsAccessor;
        this.deviceAccessor = deviceAccessor;
        this.enrollmentAccessor = enrollmentAccessor;
        this.menuService = menuService;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Pair
    //--------------------------------------------------------------------------------

    // 端末の登録。テナントと店舗は、コードかトークンで引いた受け口の行で決める (要求の中の値は使わない)
    // 公開鍵は入口で読んで書き直した JWK の JSON (読めなければ null)。店舗の書き込みの中で登録する (店舗やテーブルを使わなくする処理と重ならない)
    public async ValueTask<ServiceResult<DevicePairResponse>> PairAsync(DevicePairRequest request, string? publicKey, CancellationToken cancellationToken)
    {
        if (ValidatePair(request, publicKey) is { } invalid)
        {
            return new(invalid);
        }

        var now = contextProvider.Current.Now;
        var enrollment = !String.IsNullOrEmpty(request.PairingCode)
            ? await directoryAccessor.QueryEnrollmentByPairingCodeAsync(request.PairingCode, cancellationToken)
            : await directoryAccessor.QueryEnrollmentByTokenHashAsync(HashToken(request.EnrollmentToken ?? string.Empty), cancellationToken);
        if ((enrollment is null) || (enrollment.RevokedAt is not null) || (enrollment.ExpiresAt <= now) || (enrollment.UsedCount >= enrollment.MaxUses))
        {
            return new(new ServiceError(ErrorCodes.PairingCodeInvalid));
        }

        // 種類の違うアプリの登録は、台数を使う前に断る (誤って配ったトークンや入れ違えたコードで、使わない端末を作らない)
        if (enrollment.Kind != request.Kind)
        {
            return new(new ServiceError(ErrorCodes.DeviceKindMismatch));
        }

        var tenant = await tenantAccessor.QueryAsync(enrollment.TenantId, cancellationToken);
        if (tenant?.Status != TenantStatus.Active)
        {
            return new(new ServiceError(ErrorCodes.TenantSuspended));
        }

        var deviceId = Guid.CreateVersion7(now);
        var stationIds = String.IsNullOrEmpty(enrollment.StationIds) ? [] : JsonSerializer.Deserialize<List<Guid>>(enrollment.StationIds, JsonDefaults.Options) ?? [];
        var paired = await eventService.WriteAsync(enrollment.TenantId, enrollment.StoreId, async transaction =>
        {
            var tx = transaction.Tx;

            // 使わなくした店舗には登録しない (残っていたコードやトークンでも)
            if (await storeAccessor.QueryAsync(tx, enrollment.TenantId, enrollment.StoreId, cancellationToken) is not { IsActive: true })
            {
                return false;
            }

            // 台数の確かめと数を足すのは 1 文で行う (同時の登録で台数を超えない)
            if (await enrollmentAccessor.AddUsedCountAsync(tx, enrollment.TenantId, enrollment.Id, now, cancellationToken) == 0)
            {
                return false;
            }

            // コードを出したあとに使わなくしたテーブルには置かない (置き場所を割り当てるまで、端末は起動の画面で待つ)
            var tableId = (enrollment.TableId is { } table) && (await storeAccessor.QueryActiveTableAsync(tx, enrollment.TenantId, enrollment.StoreId, table, cancellationToken) is not null)
                ? enrollment.TableId
                : null;
            await deviceAccessor.InsertAsync(tx, enrollment.TenantId, deviceId, enrollment.StoreId, enrollment.Kind, request.DeviceName.Trim(), tableId, publicKey!, now, cancellationToken);
            foreach (var stationId in stationIds)
            {
                await deviceAccessor.InsertStationAsync(tx, enrollment.TenantId, deviceId, stationId, cancellationToken);
            }

            await deviceAccessor.UpsertStatusAsync(tx, enrollment.TenantId, deviceId, request.AppVersion, null, null, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
        if (!paired)
        {
            return new(new ServiceError(ErrorCodes.PairingCodeInvalid));
        }

        var response = new DevicePairResponse
        {
            DeviceId = deviceId,
            Kind = enrollment.Kind,
            StoreId = enrollment.StoreId
        };
        return new(response, created: true);
    }

    private static ServiceError? ValidatePair(DevicePairRequest request, string? publicKey)
    {
        var errors = new Dictionary<string, string[]>();
        if (String.IsNullOrEmpty(request.PairingCode) == String.IsNullOrEmpty(request.EnrollmentToken))
        {
            errors["pairingCode"] = ["ペアリングコードか登録トークンのどちらかを送ってください"];
        }

        if ((request.Kind is not { } kind) || !Enum.IsDefined(kind))
        {
            errors["kind"] = ["登録するアプリの端末の種類を送ってください"];
        }

        if (String.IsNullOrWhiteSpace(request.DeviceName) || (request.DeviceName.Length > Length.DeviceName))
        {
            errors["deviceName"] = [$"端末の名前を {Length.DeviceName} 文字までで送ってください"];
        }

        if (request.AppVersion?.Length > Length.AppVersion)
        {
            errors["appVersion"] = [$"アプリの版を {Length.AppVersion} 文字までで送ってください"];
        }

        if (publicKey is null)
        {
            errors["publicKey"] = ["P-256 の公開鍵 (JWK) を送ってください"];
        }

        return errors.Count > 0 ? new ServiceError(ErrorCodes.ValidationError, errors) : null;
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
    // 登録し直した端末が新しい登録でトークンを受け取るときに、同じ鍵の前の登録を無効にする
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

        await RevokePreviousAsync(device, cancellationToken);

        var stations = device.Kind == DeviceKind.Kitchen
            ? (await deviceAccessor.QueryStationListAsync(device.TenantId, device.Id, cancellationToken)).Select(static x => x.StationId).ToList()
            : [];
        return new DeviceAuthorizeResult(
            DeviceAuthorizeStatus.Success,
            new DeviceIdentity(device.TenantId, device.StoreId, device.Id, device.Kind, device.TableId, stations));
    }

    // 同じ鍵の前の登録 (登録し直す前の端末) を無効にして、その店舗に知らせる (前の行がテーブルや店舗を使ったまま残らない)
    // 登録のときではなく、鍵を持つ端末が新しい登録を使い始めたときに行う (登録の応答が届かずに前の登録を使い続ける端末と、
    // ほかの端末の公開鍵を送って作った登録で、前の登録を無効にしない)。前の登録はほかの店舗やテナントのこともあるので、店舗ごとに書く
    private async ValueTask RevokePreviousAsync(DeviceEntity device, CancellationToken cancellationToken)
    {
        var previous = (await directoryAccessor.QueryActiveDeviceListByPublicKeyAsync(device.PublicKey, cancellationToken))
            .Where(x => x.CreatedAt < device.CreatedAt)
            .ToList();
        if (previous.Count == 0)
        {
            return;
        }

        var now = contextProvider.Current.Now;
        foreach (var old in previous)
        {
            await eventService.WriteAsync(old.TenantId, old.StoreId, async transaction =>
            {
                // 管理画面で替えた直後なら (版が違う)、次のトークンの要求で無効にする
                if (await deviceAccessor.UpdateRevokedAsync(transaction.Tx, old.TenantId, old.StoreId, old.Id, old.Version, now, cancellationToken) == 0)
                {
                    return false;
                }

                await transaction.AppendEventAsync(EventTypes.DeviceUpdated, new DeviceUpdatedEventData { DeviceId = old.Id }, null, null, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }

        await revocationList.RefreshAsync(cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Me
    //--------------------------------------------------------------------------------

    // 要求した端末の状態の報告
    public async ValueTask<ServiceError?> ReportStatusAsync(DeviceHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (request.BatteryLevel is < 0m or > 1m)
        {
            return ServiceError.Validation("batteryLevel", "電池の残りは 0 から 1 で送ってください");
        }

        if (request.AppVersion?.Length > Length.AppVersion)
        {
            return ServiceError.Validation("appVersion", $"アプリの版を {Length.AppVersion} 文字までで送ってください");
        }

        var context = contextProvider.Current;
        await deviceAccessor.UpsertStatusAsync(context.RequireTenantId(), context.RequireDeviceId(), request.AppVersion, request.BatteryLevel, request.IsCharging, context.Now, cancellationToken);
        return null;
    }

    // 要求した端末の設定。置き場所はトークンではなく今の端末の記録から返す (席替えをすぐに出す)
    // チェーンの設定 (名前・ロゴ・色) はテナントの行から、機能の有無とスタッフの PIN は店舗の行から入れる
    public async ValueTask<DeviceConfigResponse?> GetConfigAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var store = await storeAccessor.QueryAsync(tenantId, context.RequireStoreId(), cancellationToken);
        var device = await deviceAccessor.QueryAsync(tenantId, context.RequireDeviceId(), cancellationToken);
        var brand = await settingsAccessor.QueryBrandAsync(tenantId, cancellationToken);
        if ((store is null) || (device is null) || (brand is null))
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
                MaxLinesPerOrder = store.MaxLinesPerOrder
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
            },
            Brand = new DeviceConfigResponseBrand
            {
                Name = brand.BrandName,
                LogoImageName = brand.LogoImageName,
                Theme = SettingsService.ReadTheme(brand.Theme)
            },
            Features = SettingsService.ReadFeatures(store.Features),
            StaffPin = UsesStaffPin(device.Kind) ? SettingsService.ReadStaffPin(store.StaffPinHash) : null,
            SettingsVersion = store.SettingsVersion
        };
    }

    // スタッフの PIN を使う端末 (スタッフメニューを持つ端末)。キッチン端末はブラウザで設定を読めるので渡さない (桁の少ない PIN はハッシュから戻せる)
    private static bool UsesStaffPin(DeviceKind kind) =>
        kind is DeviceKind.Table or DeviceKind.Hall or DeviceKind.Reception;

    //--------------------------------------------------------------------------------
    // Management
    //--------------------------------------------------------------------------------

    // 店舗の端末の一覧 (管理画面。置き場所の割り当てを待つ端末、有効な端末の順に並べる)
    public async ValueTask<List<DeviceSummaryResult>> GetSummaryListAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var devices = await deviceAccessor.QuerySummaryListAsync(tenantId, storeId, cancellationToken);
        var stations = (await deviceAccessor.QueryStationListByStoreAsync(tenantId, storeId, cancellationToken)).ToLookup(static x => x.DeviceId, static x => x.StationId);
        return devices.Select(x => new DeviceSummaryResult(x, stations[x.Id].ToList())).OrderByDescending(static x => x.IsWaitingPlacement).ToList();
    }

    // 今のメニューの持ち場 (キッチン端末の置き場所を選ぶ)
    public async ValueTask<List<MenuResponseStation>> GetStationListAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var store = await storeAccessor.QueryAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
        var catalog = store is null ? null : await menuService.GetCatalogAsync(store, cancellationToken);
        return catalog?.Menu.Stations.OrderBy(static x => x.SortOrder).ToList() ?? [];
    }

    // 端末の種類に合う置き場所か。テーブル端末は店舗の使っているテーブル、キッチン端末は今のメニューの持ち場にし、ほかの種類は置き場所を持たない
    // 置き場所は決めなくてもよい (端末は置き場所を割り当てるまで起動の画面で待つ)
    public async ValueTask<ServiceError?> ValidatePlacementAsync(DeviceKind kind, Guid? tableId, IReadOnlyList<Guid> stationIds, CancellationToken cancellationToken)
    {
        if ((tableId is not null) && (kind != DeviceKind.Table))
        {
            return ServiceError.Validation("tableId", "テーブルはテーブル端末にだけ割り当てられます");
        }

        if ((stationIds.Count > 0) && (kind != DeviceKind.Kitchen))
        {
            return ServiceError.Validation("stationIds", "持ち場はキッチン端末にだけ割り当てられます");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        if ((tableId is { } table) && (await storeAccessor.QueryActiveTableAsync(tenantId, storeId, table, cancellationToken) is null))
        {
            return ServiceError.Validation("tableId", "店舗の使っているテーブルを選んでください");
        }

        if (stationIds.Count > 0)
        {
            var known = (await GetStationListAsync(cancellationToken)).Select(static x => x.Id).ToHashSet();
            if (!stationIds.All(known.Contains))
            {
                return ServiceError.Validation("stationIds", "今のメニューの持ち場を選んでください");
            }
        }

        return null;
    }

    // 名前と置き場所を替え、端末に知らせる (端末は起動からやり直して、新しいトークンと設定を受け取る)
    // テーブルは書き込みの中でも確かめる (確かめたあとに使わなくしたテーブルに置かない)
    public async ValueTask<ServiceError?> UpdateAsync(Guid deviceId, string name, Guid? tableId, IReadOnlyList<Guid> stationIds, int version, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if ((trimmed.Length == 0) || (trimmed.Length > Length.DeviceName))
        {
            return ServiceError.Validation("name", $"名前は {Length.DeviceName} 文字までで入れてください");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var device = await deviceAccessor.QueryAsync(tenantId, deviceId, cancellationToken);
        if ((device is null) || (device.StoreId != storeId))
        {
            return ServiceError.NotFound;
        }

        if (await ValidatePlacementAsync(device.Kind, tableId, stationIds, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            if ((tableId is { } table) && (await storeAccessor.QueryActiveTableAsync(transaction.Tx, tenantId, storeId, table, cancellationToken) is null))
            {
                return ServiceError.Validation("tableId", "店舗の使っているテーブルを選んでください");
            }

            // 表示していた版でなければ (ほかで替えた、無効にした)、読み直してもらう
            if (await deviceAccessor.UpdateAsync(transaction.Tx, tenantId, storeId, deviceId, trimmed, tableId, version, context.Now, cancellationToken) == 0)
            {
                return new ServiceError(ErrorCodes.VersionMismatch);
            }

            if (device.Kind == DeviceKind.Kitchen)
            {
                await deviceAccessor.DeleteStationAsync(transaction.Tx, tenantId, deviceId, cancellationToken);
                foreach (var stationId in stationIds.Distinct())
                {
                    await deviceAccessor.InsertStationAsync(transaction.Tx, tenantId, deviceId, stationId, cancellationToken);
                }
            }

            await transaction.AppendEventAsync(EventTypes.DeviceUpdated, new DeviceUpdatedEventData { DeviceId = deviceId }, null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    // 無効にして端末に知らせ、すぐに拒む一覧を読み直す (端末の今のトークンを断り、通知の接続を切る)
    // 端末は次のトークンの要求で断られて、登録からやり直す
    public async ValueTask<ServiceError?> RevokeAsync(Guid deviceId, int version, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var error = await eventService.WriteAsync<ServiceError?>(tenantId, storeId, async transaction =>
        {
            if (await deviceAccessor.UpdateRevokedAsync(transaction.Tx, tenantId, storeId, deviceId, version, context.Now, cancellationToken) == 0)
            {
                var device = await deviceAccessor.QueryAsync(transaction.Tx, tenantId, deviceId, cancellationToken);
                return (device is not null) && (device.StoreId == storeId) ? new ServiceError(ErrorCodes.VersionMismatch) : ServiceError.NotFound;
            }

            await transaction.AppendEventAsync(EventTypes.DeviceUpdated, new DeviceUpdatedEventData { DeviceId = deviceId }, null, null, context.Now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
        if (error is null)
        {
            await revocationList.RefreshAsync(cancellationToken);
        }

        return error;
    }
}
