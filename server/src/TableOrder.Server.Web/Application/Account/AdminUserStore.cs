namespace TableOrder.Server.Web.Application.Account;

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

// ASP.NET Core Identity の利用者の置き場 (利用者の表を AccountService で読み書きする)
// 利用者を足すのと止めるのは管理画面の Service で行い、Identity からは作らない・消さない
public sealed class AdminUserStore :
    IUserPasswordStore<AdminUserEntity>,
    IUserSecurityStampStore<AdminUserEntity>,
    IUserLockoutStore<AdminUserEntity>,
    IUserTwoFactorStore<AdminUserEntity>,
    IUserAuthenticatorKeyStore<AdminUserEntity>,
    IUserTwoFactorRecoveryCodeStore<AdminUserEntity>
{
    // 認証アプリの鍵の暗号の用途 (ほかの用途の暗号と鍵を分ける)
    private const string AuthenticatorKeyPurpose = "TableOrder.Admin.AuthenticatorKey";

    private readonly ILogger<AdminUserStore> log;

    private readonly IDataProtector protector;

    private readonly AccountService accountService;

    public AdminUserStore(
        ILogger<AdminUserStore> log,
        IDataProtectionProvider dataProtectionProvider,
        AccountService accountService)
    {
        this.log = log;
        protector = dataProtectionProvider.CreateProtector(AuthenticatorKeyPurpose);
        this.accountService = accountService;
    }

    public void Dispose()
    {
    }

    //--------------------------------------------------------------------------------
    // User
    //--------------------------------------------------------------------------------

    public Task<string> GetUserIdAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    // サインインの名前はメールアドレス
    public Task<string?> GetUserNameAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(AdminUserEntity user, string? userName, CancellationToken cancellationToken)
    {
        user.Email = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedUserNameAsync(AdminUserEntity user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> CreateAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Admin users are created by the admin services.");

    // 読んだときの版でなければ書かない (同じ利用者のサインインが重なったとき)
    public async Task<IdentityResult> UpdateAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        await accountService.UpdateCredentialAsync(user, cancellationToken)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());

    public Task<IdentityResult> DeleteAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Admin users are deactivated by the admin services.");

    public async Task<AdminUserEntity?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id) ? await accountService.FindAsync(id, cancellationToken) : null;

    public async Task<AdminUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        await accountService.FindByEmailAsync(normalizedUserName, cancellationToken);

    //--------------------------------------------------------------------------------
    // Password
    //--------------------------------------------------------------------------------

    public Task SetPasswordHashAsync(AdminUserEntity user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.PasswordHash);

    public Task<bool> HasPasswordAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash.Length > 0);

    //--------------------------------------------------------------------------------
    // Security stamp
    //--------------------------------------------------------------------------------

    public Task SetSecurityStampAsync(AdminUserEntity user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.SecurityStamp);

    //--------------------------------------------------------------------------------
    // Lockout
    //--------------------------------------------------------------------------------

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(AdminUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    // 続けて間違えたら止める仕組みは、すべての利用者に使う
    public Task<bool> GetLockoutEnabledAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task SetLockoutEnabledAsync(AdminUserEntity user, bool enabled, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Two factor
    //--------------------------------------------------------------------------------

    public Task SetTwoFactorEnabledAsync(AdminUserEntity user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.TwoFactorEnabled);

    // 認証アプリの鍵は、データベースを読まれても使えないようにデータ保護で暗号にして持つ
    public Task SetAuthenticatorKeyAsync(AdminUserEntity user, string key, CancellationToken cancellationToken)
    {
        user.AuthenticatorKey = protector.Protect(key);
        return Task.CompletedTask;
    }

    // データ保護の鍵をなくしたなどで読めない鍵は、ないものとする (利用者は登録し直す)
    public Task<string?> GetAuthenticatorKeyAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        if (user.AuthenticatorKey is null)
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            return Task.FromResult<string?>(protector.Unprotect(user.AuthenticatorKey));
        }
        catch (CryptographicException ex)
        {
            log.WarnAuthenticatorKeyUnreadable(ex, user.Id);
            return Task.FromResult<string?>(null);
        }
    }

    // 回復用のコードは、パスワードと同じく元に戻せない形 (ハッシュ) で持つ
    public Task ReplaceCodesAsync(AdminUserEntity user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        user.RecoveryCodes = JsonSerializer.Serialize(recoveryCodes.Select(HashRecoveryCode).ToList());
        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(AdminUserEntity user, string code, CancellationToken cancellationToken)
    {
        var hashes = ReadCodes(user);
        if (!hashes.Remove(HashRecoveryCode(code)))
        {
            return Task.FromResult(false);
        }

        user.RecoveryCodes = JsonSerializer.Serialize(hashes);
        return Task.FromResult(true);
    }

    public Task<int> CountCodesAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(ReadCodes(user).Count);

    private static List<string> ReadCodes(AdminUserEntity user) =>
        user.RecoveryCodes is null ? [] : JsonSerializer.Deserialize<List<string>>(user.RecoveryCodes) ?? [];

    private static string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));
}
