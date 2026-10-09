namespace TableOrder.Server.Core.Accessors;

// 管理画面のサインインと、利用者が自分の資格情報を読み書きするところ (利用者の id かメールアドレスで引く)。運営者の管理もここで行う
// 利用者の表は運営者を含むのでテナントの外に置き、サインインの前はテナントもわからないので、テナントの条件を調べるテストから外す
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class AccountAccessor
{
    [QueryFirst]
    public partial ValueTask<AdminUserEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AdminUserEntity?> QueryByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<AdminUserStoreEntity>> QueryStoreListAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountOperatorAsync(CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(Guid id, Guid? tenantId, AdminRole role, string email, string normalizedEmail, string name, string passwordHash, bool mustChangePassword, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    // 読んだときの版のときだけ書く (0 件ならほかで替えたので書かない)
    [Execute]
    public partial ValueTask<int> UpdateCredentialAsync(Guid id, string passwordHash, bool mustChangePassword, string securityStamp, int accessFailedCount, DateTimeOffset? lockoutEnd, bool twoFactorEnabled, string? authenticatorKey, string? recoveryCodes, DateTimeOffset now, int version, CancellationToken cancellationToken);

    // 最後のサインインは資格情報ではないので、版を上げない (開いているサインインの処理の版をずらさない)
    [Execute]
    public partial ValueTask<int> UpdateSignedInAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Operator
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<AdminUserSummaryEntity>> QueryOperatorListAsync(CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AdminUserSummaryEntity?> QueryOperatorAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorAsync(Guid id, string name, string securityStamp, DateTimeOffset now, int version, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorPasswordAsync(Guid id, string passwordHash, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorTwoFactorResetAsync(Guid id, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorActiveAsync(Guid id, bool isActive, string securityStamp, DateTimeOffset now, int version, CancellationToken cancellationToken);
}
