namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// 管理画面のサインインと、利用者が自分の資格情報を読み書きする処理 (ASP.NET Core Identity の利用者の置き場から呼ぶ)
// サインインの前はテナントがわからないので、要求の文脈を使わずに利用者の id かメールアドレスで引く
public sealed class AccountService
{
    private readonly TimeProvider timeProvider;

    private readonly AccountAccessor accountAccessor;

    private readonly TenantAccessor tenantAccessor;

    public AccountService(
        TimeProvider timeProvider,
        AccountAccessor accountAccessor,
        TenantAccessor tenantAccessor)
    {
        this.timeProvider = timeProvider;
        this.accountAccessor = accountAccessor;
        this.tenantAccessor = tenantAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<AdminUserEntity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        accountAccessor.QueryAsync(id, cancellationToken);

    public ValueTask<AdminUserEntity?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        accountAccessor.QueryByNormalizedEmailAsync(normalizedEmail, cancellationToken);

    // 店舗の担当が受け持つ店舗 (ほかの役割は店舗を限らない)
    public async ValueTask<IReadOnlyList<Guid>> GetStoreIdsAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        if ((user.Role != AdminRole.StoreStaff) || (user.TenantId is not { } tenantId))
        {
            return [];
        }

        var stores = await accountAccessor.QueryStoreListAsync(tenantId, user.Id, cancellationToken);
        return stores.Select(static x => x.StoreId).ToList();
    }

    // 止めた利用者と、止めたテナントの利用者はサインインできない
    public async ValueTask<bool> CanSignInAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            return false;
        }

        if (user.TenantId is not { } tenantId)
        {
            return true;
        }

        var tenant = await tenantAccessor.QueryAsync(tenantId, cancellationToken);
        return tenant?.Status == TenantStatus.Active;
    }

    public async ValueTask<bool> HasOperatorAsync(CancellationToken cancellationToken) =>
        await accountAccessor.CountOperatorAsync(cancellationToken) > 0;

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 資格情報 (パスワード、印、間違えた回数、多要素) を書く。読んだときの版でなければ書かずに false を返す
    // 止めた利用者の資格情報は書かない (止める前のサインインのまま、パスワードを替えて Cookie を出し直させない)
    public async ValueTask<bool> UpdateCredentialAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await accountAccessor.UpdateCredentialAsync(
            user.Id,
            user.PasswordHash,
            user.MustChangePassword,
            user.SecurityStamp,
            user.AccessFailedCount,
            user.LockoutEnd,
            user.TwoFactorEnabled,
            user.AuthenticatorKey,
            user.RecoveryCodes,
            now,
            user.Version,
            cancellationToken);
        if (updated == 0)
        {
            return false;
        }

        user.UpdatedAt = now;
        user.Version++;
        return true;
    }

    public async ValueTask RecordSignInAsync(Guid id, CancellationToken cancellationToken) =>
        await accountAccessor.UpdateSignedInAsync(id, timeProvider.GetUtcNow(), cancellationToken);

    // 初めの運営者 (運営者がひとりもいないときに、起動のときに設定から作る。次のサインインでパスワードを替えさせる)
    public async ValueTask<bool> CreateInitialOperatorAsync(string email, string normalizedEmail, string name, string passwordHash, CancellationToken cancellationToken)
    {
        if (await HasOperatorAsync(cancellationToken))
        {
            return false;
        }

        await accountAccessor.InsertAsync(
            Guid.CreateVersion7(),
            null,
            AdminRole.Operator,
            email,
            normalizedEmail,
            name,
            passwordHash,
            true,
            Guid.NewGuid().ToString("N"),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return true;
    }
}
