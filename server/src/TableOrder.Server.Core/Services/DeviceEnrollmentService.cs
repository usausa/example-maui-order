namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 端末の登録の受け口を出す (管理画面から呼ぶ。テナントと店舗は管理画面が選んだもの)
public sealed class DeviceEnrollmentService
{
    // ペアリングコードは 10 分、1 台
    private static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    // コードはすべてのテナントで一意にするので、重なったら出し直す
    private const int MaxAttempts = 10;

    private readonly TimeProvider timeProvider;

    private readonly IDialect dialect;

    private readonly DeviceEnrollmentAccessor enrollmentAccessor;

    public DeviceEnrollmentService(
        TimeProvider timeProvider,
        IDialect dialect,
        DeviceEnrollmentAccessor enrollmentAccessor)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.enrollmentAccessor = enrollmentAccessor;
    }

    public async ValueTask<string> IssuePairingCodeAsync(Guid tenantId, Guid storeId, DeviceKind kind, Guid? tableId, IReadOnlyList<Guid> stationIds, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var stations = stationIds.Count > 0 ? JsonSerializer.Serialize(stationIds, JsonDefaults.Options) : null;
        for (var i = 0; i < MaxAttempts; i++)
        {
            var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, Length.PairingCodeDigits)).ToString(new string('0', Length.PairingCodeDigits), CultureInfo.InvariantCulture);
            try
            {
                await enrollmentAccessor.InsertAsync(
                    tenantId, Guid.CreateVersion7(now), storeId, kind, EnrollmentMethod.PairingCode, code, null, tableId, stations, 1, now + PairingCodeLifetime, now, cancellationToken);
                return code;
            }
            catch (DbException e) when (dialect.IsDuplicate(e))
            {
                // 期限の内のコードと重なった
            }
        }

        throw new InvalidOperationException("Pairing code could not be issued.");
    }
}
