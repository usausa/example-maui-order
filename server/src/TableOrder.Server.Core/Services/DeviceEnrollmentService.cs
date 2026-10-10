namespace TableOrder.Server.Core.Services;

using System.Buffers.Text;
using System.Security.Cryptography;

using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

// 出したペアリングコードと期限
public sealed record PairingCodeResult(string Code, DateTimeOffset ExpiresAt);

// 出した登録トークンと期限 (トークンの平文を返すのは出したときだけ)
public sealed record EnrollmentTokenResult(Guid Id, string Token, DateTimeOffset ExpiresAt);

// 端末の登録の受け口を出す (管理画面から、選んだ店舗の文脈で呼ぶ)
public sealed class DeviceEnrollmentService
{
    // 登録トークンの台数と期限 (日) の上限
    public const int MaxTokenUses = 1000;

    public const int MaxTokenDays = 30;

    // ペアリングコードは 10 分、1 台
    private static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    // 登録トークンは、期限を過ぎるか取り消してから 1 日は一覧に出す
    private static readonly TimeSpan TokenRetention = TimeSpan.FromDays(1);

    // コードはすべてのテナントで一意にするので、重なったら出し直す
    private const int MaxAttempts = 10;

    // 登録トークンの乱数のバイト数 (推測できない長さにする)
    private const int TokenBytes = 32;

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

    //--------------------------------------------------------------------------------
    // Pairing code
    //--------------------------------------------------------------------------------

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

    //--------------------------------------------------------------------------------
    // Enrollment token
    //--------------------------------------------------------------------------------

    // 端末の種類と台数・期限を決めた登録トークンを出す (EMM で端末に配る)。置き場所は、登録したあとに端末の画面で割り当てる
    // トークンはハッシュだけを持ち、平文は返したあとに読めない
    public async ValueTask<ServiceResult<EnrollmentTokenResult>> IssueEnrollmentTokenAsync(DeviceKind kind, int maxUses, int days, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
        {
            return new(ServiceError.Validation("kind", "端末の種類を選んでください"));
        }

        if (maxUses is < 1 or > MaxTokenUses)
        {
            return new(ServiceError.Validation("maxUses", $"台数は 1 から {MaxTokenUses} で入れてください"));
        }

        if (days is < 1 or > MaxTokenDays)
        {
            return new(ServiceError.Validation("days", $"期限は 1 から {MaxTokenDays} 日で入れてください"));
        }

        var context = contextProvider.Current;
        var now = timeProvider.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var expiresAt = now.AddDays(days);
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        await enrollmentAccessor.InsertAsync(
            context.RequireTenantId(), id, context.RequireStoreId(), kind, EnrollmentMethod.EnrollmentToken, null, DeviceService.HashToken(token), null, null, maxUses, expiresAt, now, cancellationToken);
        return new(new EnrollmentTokenResult(id, token, expiresAt), true);
    }

    // 店舗の登録トークン (新しいものから。使い切ったもの、期限を過ぎたもの、取り消したものも、消すまで出す)
    public ValueTask<List<DeviceEnrollmentEntity>> GetTokenListAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return enrollmentAccessor.QueryTokenListAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
    }

    // 取り消して、それからの登録を断る (登録した端末はそのまま)。取り消してあったものは成功にする (2 つの画面で同じトークンを取り消す)
    public async ValueTask<ServiceError?> RevokeTokenAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        if (await enrollmentAccessor.UpdateRevokedAsync(tenantId, storeId, id, timeProvider.GetUtcNow(), cancellationToken) > 0)
        {
            return null;
        }

        return (await enrollmentAccessor.QueryTokenListAsync(tenantId, storeId, cancellationToken)).Exists(x => (x.Id == id) && (x.RevokedAt is not null))
            ? null
            : ServiceError.NotFound;
    }

    //--------------------------------------------------------------------------------
    // Cleanup
    //--------------------------------------------------------------------------------

    // 期限を過ぎたペアリングコードと、期限を過ぎるか取り消してから 1 日たった登録トークンを消す (裏の処理から呼ぶ)
    public async ValueTask<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await backgroundAccessor.DeletePairingCodeAsync(now, cancellationToken) +
        await backgroundAccessor.DeleteEnrollmentTokenAsync(now - TokenRetention, cancellationToken);
}
