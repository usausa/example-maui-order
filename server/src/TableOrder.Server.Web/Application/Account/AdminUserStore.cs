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
    // 認証アプリの鍵と回復用のコードの暗号の用途 (ほかの用途の暗号と鍵を分ける)
    private const string AuthenticatorKeyPurpose = "TableOrder.Admin.AuthenticatorKey";

    private const string RecoveryCodePurpose = "TableOrder.Admin.RecoveryCode";

    private readonly ILogger<AdminUserStore> log;

    private readonly IDataProtector protector;

    private readonly IDataProtector codeProtector;

    private readonly AccountService accountService;

    public AdminUserStore(
        ILogger<AdminUserStore> log,
        IDataProtectionProvider dataProtectionProvider,
        AccountService accountService)
    {
        this.log = log;
        protector = dataProtectionProvider.CreateProtector(AuthenticatorKeyPurpose);
        codeProtector = dataProtectionProvider.CreateProtector(RecoveryCodePurpose);
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

    // 間違えた回数と止める時刻は、資格情報の書き込み (UpdateAsync。版を見る) と分けて、すぐに 1 文で書く
    // (同時に間違えたサインインは版が違って書けず、数え漏らす。止めたことも、ほかの書き込みで消さない)

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnd);

    public async Task SetLockoutEndDateAsync(AdminUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        await accountService.SetLockoutEndAsync(user.Id, lockoutEnd, cancellationToken);
    }

    public async Task<int> IncrementAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = await accountService.AddAccessFailedCountAsync(user.Id, cancellationToken);
        return user.AccessFailedCount;
    }

    public async Task ResetAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        await accountService.ResetAccessFailedCountAsync(user.Id, cancellationToken);
    }

    // 読んだあとにほかのサインインが数えていることがあるので、今の数を読む (正しいパスワードのときに戻し損ねないように)
    public async Task<int> GetAccessFailedCountAsync(AdminUserEntity user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = await accountService.GetAccessFailedCountAsync(user.Id, cancellationToken);
        return user.AccessFailedCount;
    }

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

    // 回復用のコードは、認証アプリの鍵と同じくデータ保護の鍵で暗号にして持つ (DB だけが漏れても使えない)
    // 桁の少ないコードを塩のないハッシュで持つと、すべての利用者の分を一度に総当たりで戻せる
    public Task ReplaceCodesAsync(AdminUserEntity user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        user.RecoveryCodes = WriteCodes(recoveryCodes.Select(static x => x.Trim()));
        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(AdminUserEntity user, string code, CancellationToken cancellationToken)
    {
        var codes = ReadCodes(user);
        var entered = Encoding.UTF8.GetBytes(code.Trim());
        var index = codes.FindIndex(x => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(x), entered));
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        codes.RemoveAt(index);
        user.RecoveryCodes = WriteCodes(codes);
        return Task.FromResult(true);
    }

    public Task<int> CountCodesAsync(AdminUserEntity user, CancellationToken cancellationToken) =>
        Task.FromResult(ReadCodes(user).Count);

    // 読めないコード (データ保護の鍵をなくした、前の形のハッシュ) は、ないものとする (利用者は出し直す)
    private List<string> ReadCodes(AdminUserEntity user)
    {
        if (user.RecoveryCodes is null)
        {
            return [];
        }

        var codes = new List<string>();
        foreach (var value in JsonSerializer.Deserialize<List<string>>(user.RecoveryCodes) ?? [])
        {
            try
            {
                codes.Add(codeProtector.Unprotect(value));
            }
            catch (CryptographicException)
            {
                // 読めないものは使わない
            }
        }

        return codes;
    }

    private string WriteCodes(IEnumerable<string> codes) =>
        JsonSerializer.Serialize(codes.Select(codeProtector.Protect).ToList());
}
