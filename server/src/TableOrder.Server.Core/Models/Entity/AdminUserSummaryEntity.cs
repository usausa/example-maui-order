namespace TableOrder.Server.Core.Models.Entity;

// 管理画面の利用者の一覧の行 (資格情報 (パスワードのハッシュ、認証アプリの鍵、回復用のコード) は読まない)
public sealed class AdminUserSummaryEntity
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public AdminRole Role { get; set; }

    public string Email { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool MustChangePassword { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset? LastSignInAt { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; }
}
