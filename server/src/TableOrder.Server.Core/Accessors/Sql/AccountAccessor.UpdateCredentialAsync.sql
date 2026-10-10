UPDATE
    AdminUsers
SET
    PasswordHash = /*@ passwordHash */'',
    MustChangePassword = /*@ mustChangePassword */0,
    SecurityStamp = /*@ securityStamp */'',
    TwoFactorEnabled = /*@ twoFactorEnabled */0,
    AuthenticatorKey = /*@ authenticatorKey */NULL,
    RecoveryCodes = /*@ recoveryCodes */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND IsActive = 1
    AND Version = /*@ version */0
