namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// 利用者の一覧の行と、受け持つ店舗 (店舗の担当だけ)
public sealed record AdminUserListItem(AdminUserSummaryEntity User, IReadOnlyList<Guid> StoreIds);

// 足す利用者 (メールアドレスは大文字にそろえたものも渡す)
public sealed record AdminUserInput(string Email, string NormalizedEmail, string Name, AdminRole Role, IReadOnlyList<Guid> StoreIds);

// テナントの利用者 (テナントの管理者と店舗の担当) の管理 (管理画面から、選んだテナントの文脈で呼ぶ)
// パスワードは管理画面がハッシュにして渡す。役割・受け持つ店舗・パスワード・多要素・止めることを替えたら資格の印を替え、開いている管理画面をやり直させる
public sealed class AdminUserService
{
    public const int MaxNameLength = 50;

    public const int MaxEmailLength = 254;

    private readonly ServiceContextProvider contextProvider;

    private readonly IDbProvider provider;

    private readonly IDialect dialect;

    private readonly AdminUserAccessor adminUserAccessor;

    private readonly StoreAccessor storeAccessor;

    public AdminUserService(
        ServiceContextProvider contextProvider,
        IDbProvider provider,
        IDialect dialect,
        AdminUserAccessor adminUserAccessor,
        StoreAccessor storeAccessor)
    {
        this.contextProvider = contextProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.adminUserAccessor = adminUserAccessor;
        this.storeAccessor = storeAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public async ValueTask<List<AdminUserListItem>> GetListAsync(CancellationToken cancellationToken)
    {
        var tenantId = contextProvider.Current.RequireTenantId();
        var users = await adminUserAccessor.QueryListAsync(tenantId, cancellationToken);
        var stores = (await adminUserAccessor.QueryStoreListAsync(tenantId, cancellationToken)).ToLookup(static x => x.UserId, static x => x.StoreId);
        return users.Select(x => new AdminUserListItem(x, stores[x.Id].ToList())).ToList();
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 仮のパスワードの利用者を足す (次のサインインでパスワードを替えさせる)
    public async ValueTask<ServiceResult<AdminUserSummaryEntity>> AddAsync(AdminUserInput input, string passwordHash, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var email = input.Email.Trim();
        if ((email.Length == 0) || (email.Length > MaxEmailLength))
        {
            return new(ServiceError.Validation("email", "メールアドレスを入れてください"));
        }

        if (await ValidateAsync(tenantId, input.Name, input.Role, input.StoreIds, cancellationToken) is { } invalid)
        {
            return new(invalid);
        }

        var id = Guid.CreateVersion7(context.Now);
        try
        {
            await provider.UsingTxAsync(async (_, tx) =>
            {
                await adminUserAccessor.InsertAsync(tx, tenantId, id, input.Role, email, input.NormalizedEmail, input.Name.Trim(), passwordHash, NewStamp(), context.Now, cancellationToken);
                await InsertStoresAsync(tx, tenantId, id, input.Role, input.StoreIds, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            // メールアドレスはすべてのテナントで一意 (サインインの名前)
            return new(ServiceError.Validation("email", "このメールアドレスはほかの利用者が使っています"));
        }

        return new((await adminUserAccessor.QueryAsync(tenantId, id, cancellationToken))!, true);
    }

    // 名前・役割・受け持つ店舗を替える。自分の役割は替えない (自分で管理者でなくなって戻れなくならないように)
    public async ValueTask<ServiceError?> UpdateAsync(Guid userId, string name, AdminRole role, IReadOnlyList<Guid> storeIds, int version, Guid actorId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        if (await ValidateAsync(tenantId, name, role, storeIds, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var user = await adminUserAccessor.QueryAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            return ServiceError.NotFound;
        }

        if ((userId == actorId) && (role != user.Role))
        {
            return ServiceError.Validation("role", "自分の役割は替えられません");
        }

        return await provider.UsingTxAsync<ServiceError?>(async (_, tx) =>
        {
            // 表示していた版でなければ (ほかで替えた)、読み直してもらう
            if (await adminUserAccessor.UpdateAsync(tx, tenantId, userId, role, name.Trim(), NewStamp(), context.Now, version, cancellationToken) == 0)
            {
                return new ServiceError(ErrorCodes.VersionMismatch);
            }

            await adminUserAccessor.DeleteStoreAsync(tx, tenantId, userId, cancellationToken);
            await InsertStoresAsync(tx, tenantId, userId, role, storeIds, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return null;
        }, cancellationToken);
    }

    // 仮のパスワードにする (パスワードを忘れたとき。続けて間違えて止めていたら、それも戻す)
    public async ValueTask<ServiceError?> ResetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return await adminUserAccessor.UpdatePasswordAsync(context.RequireTenantId(), userId, passwordHash, NewStamp(), context.Now, cancellationToken) == 0
            ? ServiceError.NotFound
            : null;
    }

    // 多要素を外す (認証アプリをなくしたとき)
    public async ValueTask<ServiceError?> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return await adminUserAccessor.UpdateTwoFactorResetAsync(context.RequireTenantId(), userId, NewStamp(), context.Now, cancellationToken) == 0
            ? ServiceError.NotFound
            : null;
    }

    // 止める・戻す。自分は止めない
    public async ValueTask<ServiceError?> SetActiveAsync(Guid userId, bool isActive, int version, Guid actorId, CancellationToken cancellationToken)
    {
        if (!isActive && (userId == actorId))
        {
            return ServiceError.Validation("isActive", "自分は止められません");
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        if (await adminUserAccessor.UpdateActiveAsync(tenantId, userId, isActive, NewStamp(), context.Now, version, cancellationToken) == 0)
        {
            return await adminUserAccessor.QueryAsync(tenantId, userId, cancellationToken) is null ? ServiceError.NotFound : new ServiceError(ErrorCodes.VersionMismatch);
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async ValueTask<ServiceError?> ValidateAsync(Guid tenantId, string name, AdminRole role, IReadOnlyList<Guid> storeIds, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if ((trimmed.Length == 0) || (trimmed.Length > MaxNameLength))
        {
            return ServiceError.Validation("name", $"名前は {MaxNameLength} 文字までで入れてください");
        }

        if (role is not (AdminRole.TenantAdmin or AdminRole.StoreStaff))
        {
            return ServiceError.Validation("role", "テナントの管理者か店舗の担当を選んでください");
        }

        if (role != AdminRole.StoreStaff)
        {
            return null;
        }

        if (storeIds.Count == 0)
        {
            return ServiceError.Validation("storeIds", "店舗の担当は受け持つ店舗を選んでください");
        }

        var stores = (await storeAccessor.QueryAllAsync(tenantId, cancellationToken)).Select(static x => x.Id).ToHashSet();
        return storeIds.All(stores.Contains) ? null : ServiceError.Validation("storeIds", "テナントの店舗を選んでください");
    }

    // テナントの管理者はすべての店舗を扱うので、受け持つ店舗は店舗の担当にだけ持つ
    private async ValueTask InsertStoresAsync(DbTransaction tx, Guid tenantId, Guid userId, AdminRole role, IEnumerable<Guid> storeIds, CancellationToken cancellationToken)
    {
        if (role != AdminRole.StoreStaff)
        {
            return;
        }

        foreach (var storeId in storeIds.Distinct())
        {
            await adminUserAccessor.InsertStoreAsync(tx, tenantId, userId, storeId, cancellationToken);
        }
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
