namespace TableOrder.Server.Core.Models.Entity;

// 管理画面の利用者。運営者はテナントに属さないので、Tenants と同じくテナントの外の表にする (TenantId は運営者のとき null)
public sealed class AdminUserEntity
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public AdminRole Role { get; set; }

    // サインインの名前と、大文字にそろえたもの (すべてのテナントで一意)
    public string Email { get; set; } = default!;

    public string NormalizedEmail { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    // 管理者が出した仮のパスワード (次のサインインで替えさせる)
    public bool MustChangePassword { get; set; }

    // 資格情報を替えたら替える印 (開いている管理画面をやり直させる)
    public string SecurityStamp { get; set; } = default!;

    public int AccessFailedCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool TwoFactorEnabled { get; set; }

    // 認証アプリの鍵 (データ保護で暗号にしたもの)
    public string? AuthenticatorKey { get; set; }

    // 回復用のコードのハッシュ (JSON の配列)
    public string? RecoveryCodes { get; set; }

    public DateTimeOffset? LastSignInAt { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
