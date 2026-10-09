namespace TableOrder.Server.Core.Accessors;

// テナントの利用者 (テナントの管理者と店舗の担当) の管理。運営者 (テナントに属さない) は AccountAccessor で扱う
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class AdminUserAccessor
{
    [Query]
    public partial ValueTask<List<AdminUserSummaryEntity>> QueryListAsync(Guid tenantId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<AdminUserSummaryEntity?> QueryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<AdminUserStoreEntity>> QueryStoreListAsync(Guid tenantId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, Guid tenantId, Guid id, AdminRole role, string email, string normalizedEmail, string name, string passwordHash, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> InsertStoreAsync(DbTransaction tx, Guid tenantId, Guid userId, Guid storeId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteStoreAsync(DbTransaction tx, Guid tenantId, Guid userId, CancellationToken cancellationToken);

    // 役割と名前を替える (読んだときの版のときだけ。資格の印も替えて、開いている管理画面をやり直させる)
    [Execute]
    public partial ValueTask<int> UpdateAsync(DbTransaction tx, Guid tenantId, Guid id, AdminRole role, string name, string securityStamp, DateTimeOffset now, int version, CancellationToken cancellationToken);

    // 仮のパスワードにする (次のサインインで替えさせ、続けて間違えた回数と止めた期限を戻す)
    [Execute]
    public partial ValueTask<int> UpdatePasswordAsync(Guid tenantId, Guid id, string passwordHash, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    // 多要素を外す (認証アプリをなくしたとき。鍵と回復用のコードも捨てる)
    [Execute]
    public partial ValueTask<int> UpdateTwoFactorResetAsync(Guid tenantId, Guid id, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateActiveAsync(Guid tenantId, Guid id, bool isActive, string securityStamp, DateTimeOffset now, int version, CancellationToken cancellationToken);

    // テナントのすべての利用者の資格の印を替える (テナントを止めたとき、開いている管理画面をやり直させる)
    [Execute]
    public partial ValueTask<int> UpdateStampAllAsync(DbTransaction tx, Guid tenantId, string securityStamp, DateTimeOffset now, CancellationToken cancellationToken);
}
