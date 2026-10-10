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

    // 読んだときの版のときだけ書く (0 件ならほかで替えたので書かない)。間違えた回数と止める時刻は別に書く
    [Execute]
    public partial ValueTask<int> UpdateCredentialAsync(Guid id, string passwordHash, bool mustChangePassword, string securityStamp, bool twoFactorEnabled, string? authenticatorKey, string? recoveryCodes, DateTimeOffset now, int version, CancellationToken cancellationToken);

    // サインインの失敗を数える。版を見ずに 1 文で足して、足したあとの数を返す (同時に間違えたサインインを数え漏らさない)
    [ExecuteScalar]
    public partial ValueTask<int> AddAccessFailedCountAsync(Guid id, CancellationToken cancellationToken);

    // 今の間違えた回数 (読んだあとにほかのサインインが数えていることがある)
    [ExecuteScalar]
    public partial ValueTask<int> QueryAccessFailedCountAsync(Guid id, CancellationToken cancellationToken);

    // 間違えた回数を戻す (正しいパスワードのときと、止めたとき)
    [Execute]
    public partial ValueTask<int> UpdateAccessFailedCountAsync(Guid id, int accessFailedCount, CancellationToken cancellationToken);

    // 止める時刻は版を見ずに書く (同時に書いたほかの資格情報の書き込みで、止めたことを消さない)
    [Execute]
    public partial ValueTask<int> UpdateLockoutEndAsync(Guid id, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken);

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
    public partial ValueTask<int> UpdateOperatorAsync(Guid id, string name, DateTimeOffset now, int version, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorPasswordAsync(Guid id, string passwordHash, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorTwoFactorResetAsync(Guid id, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateOperatorActiveAsync(Guid id, bool isActive, string securityStamp, DateTimeOffset now, int version, CancellationToken cancellationToken);
}
