namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

// 運営者 (テナントに属さない利用者) の管理 (運営者の管理画面から呼ぶ)
// パスワードは管理画面がハッシュにして渡す。パスワード・多要素・止めることを替えたら資格の印を替え、開いている管理画面をやり直させる (名前だけなら替えない)
public sealed class OperatorService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IDialect dialect;

    private readonly AccountAccessor accountAccessor;

    public OperatorService(
        ServiceContextProvider contextProvider,
        IDialect dialect,
        AccountAccessor accountAccessor)
    {
        this.contextProvider = contextProvider;
        this.dialect = dialect;
        this.accountAccessor = accountAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public ValueTask<List<AdminUserSummaryEntity>> GetListAsync(CancellationToken cancellationToken) =>
        accountAccessor.QueryOperatorListAsync(cancellationToken);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 仮のパスワードの運営者を足す (次のサインインでパスワードを替えさせる)
    public async ValueTask<ServiceResult<AdminUserSummaryEntity>> AddAsync(string email, string normalizedEmail, string name, string passwordHash, CancellationToken cancellationToken)
    {
        var trimmedEmail = email.Trim();
        if ((trimmedEmail.Length == 0) || (trimmedEmail.Length > AdminUserService.MaxEmailLength))
        {
            return new(ServiceError.Validation("email", "メールアドレスを入れてください"));
        }

        if (!AdminUserService.IsEmail(trimmedEmail))
        {
            return new(ServiceError.Validation("email", "メールアドレスの形で入れてください"));
        }

        var trimmedName = name.Trim();
        if ((trimmedName.Length == 0) || (trimmedName.Length > AdminUserService.MaxNameLength))
        {
            return new(ServiceError.Validation("name", $"名前は {AdminUserService.MaxNameLength} 文字までで入れてください"));
        }

        var context = contextProvider.Current;
        var id = Guid.CreateVersion7(context.Now);
        try
        {
            await accountAccessor.InsertAsync(id, null, AdminRole.Operator, trimmedEmail, normalizedEmail, trimmedName, passwordHash, true, NewStamp(), context.Now, cancellationToken);
        }
        catch (DbException e) when (dialect.IsDuplicate(e))
        {
            return new(ServiceError.Validation("email", "このメールアドレスはほかの利用者が使っています"));
        }

        return new((await accountAccessor.QueryOperatorAsync(id, cancellationToken))!, true);
    }

    public async ValueTask<ServiceError?> UpdateAsync(Guid userId, string name, int version, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if ((trimmed.Length == 0) || (trimmed.Length > AdminUserService.MaxNameLength))
        {
            return ServiceError.Validation("name", $"名前は {AdminUserService.MaxNameLength} 文字までで入れてください");
        }

        if (await accountAccessor.UpdateOperatorAsync(userId, trimmed, contextProvider.Current.Now, version, cancellationToken) == 0)
        {
            return await NotFoundOrVersionMismatchAsync(userId, cancellationToken);
        }

        return null;
    }

    public async ValueTask<ServiceError?> ResetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken) =>
        await accountAccessor.UpdateOperatorPasswordAsync(userId, passwordHash, NewStamp(), contextProvider.Current.Now, cancellationToken) == 0
            ? ServiceError.NotFound
            : null;

    public async ValueTask<ServiceError?> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken) =>
        await accountAccessor.UpdateOperatorTwoFactorResetAsync(userId, NewStamp(), contextProvider.Current.Now, cancellationToken) == 0
            ? ServiceError.NotFound
            : null;

    // 止める・戻す。自分は止めない (運営者がいなくならないように)
    public async ValueTask<ServiceError?> SetActiveAsync(Guid userId, bool isActive, int version, Guid actorId, CancellationToken cancellationToken)
    {
        if (!isActive && (userId == actorId))
        {
            return ServiceError.Validation("isActive", "自分は止められません");
        }

        if (await accountAccessor.UpdateOperatorActiveAsync(userId, isActive, NewStamp(), contextProvider.Current.Now, version, cancellationToken) == 0)
        {
            return await NotFoundOrVersionMismatchAsync(userId, cancellationToken);
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async ValueTask<ServiceError> NotFoundOrVersionMismatchAsync(Guid userId, CancellationToken cancellationToken) =>
        await accountAccessor.QueryOperatorAsync(userId, cancellationToken) is null ? ServiceError.NotFound : new ServiceError(ErrorCodes.VersionMismatch);

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
