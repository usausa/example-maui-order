UPDATE
    AdminUsers
SET
    PasswordHash = /*@ passwordHash */'',
    MustChangePassword = /*@ mustChangePassword */0,
    SecurityStamp = /*@ securityStamp */'',
    AccessFailedCount = /*@ accessFailedCount */0,
    LockoutEnd = /*@ lockoutEnd */NULL,
    TwoFactorEnabled = /*@ twoFactorEnabled */0,
    AuthenticatorKey = /*@ authenticatorKey */NULL,
    RecoveryCodes = /*@ recoveryCodes */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND IsActive = 1
    AND Version = /*@ version */0
