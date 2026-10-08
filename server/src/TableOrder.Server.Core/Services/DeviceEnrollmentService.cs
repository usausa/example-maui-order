namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 出したペアリングコードと期限
public sealed record PairingCodeResult(string Code, DateTimeOffset ExpiresAt);

// 端末の登録の受け口を出す (管理画面から、選んだ店舗の文脈で呼ぶ)
public sealed class DeviceEnrollmentService
{
    // ペアリングコードは 10 分、1 台
    private static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    // コードはすべてのテナントで一意にするので、重なったら出し直す
    private const int MaxAttempts = 10;

    private readonly TimeProvider timeProvider;

    private readonly ServiceContextProvider contextProvider;

    private readonly IDialect dialect;

    private readonly DeviceEnrollmentAccessor enrollmentAccessor;

    private readonly BackgroundAccessor backgroundAccessor;

    private readonly DeviceService deviceService;

    public DeviceEnrollmentService(
        TimeProvider timeProvider,
        ServiceContextProvider contextProvider,
        IDialect dialect,
        DeviceEnrollmentAccessor enrollmentAccessor,
        BackgroundAccessor backgroundAccessor,
        DeviceService deviceService)
    {
        this.timeProvider = timeProvider;
        this.contextProvider = contextProvider;
        this.dialect = dialect;
        this.enrollmentAccessor = enrollmentAccessor;
        this.backgroundAccessor = backgroundAccessor;
        this.deviceService = deviceService;
    }

    // 端末の種類と置き場所を決めたペアリングコードを出す (置き場所は登録のあとに替えてもよいので、決めなくてもよい)
    public async ValueTask<ServiceResult<PairingCodeResult>> IssuePairingCodeAsync(DeviceKind kind, Guid? tableId, IReadOnlyList<Guid> stationIds, CancellationToken cancellationToken)
    {
        if (await deviceService.ValidatePlacementAsync(kind, tableId, stationIds, cancellationToken) is { } invalid)
        {
            return new(invalid);
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + PairingCodeLifetime;
        var stations = stationIds.Count > 0 ? JsonSerializer.Serialize(stationIds.Distinct().ToList(), JsonDefaults.Options) : null;
        for (var i = 0; i < MaxAttempts; i++)
        {
            var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, Length.PairingCodeDigits)).ToString(new string('0', Length.PairingCodeDigits), CultureInfo.InvariantCulture);
            try
            {
                await enrollmentAccessor.InsertAsync(
                    tenantId, Guid.CreateVersion7(now), storeId, kind, EnrollmentMethod.PairingCode, code, null, tableId, stations, 1, expiresAt, now, cancellationToken);
                return new(new PairingCodeResult(code, expiresAt), true);
            }
            catch (DbException e) when (dialect.IsDuplicate(e))
            {
                // 期限の内のコードと重なった
            }
        }

        throw new InvalidOperationException("Pairing code could not be issued.");
    }

    // 期限を過ぎたペアリングコードを消す (裏の処理から呼ぶ)
    public ValueTask<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        backgroundAccessor.DeleteEnrollmentAsync(now, cancellationToken);
}
