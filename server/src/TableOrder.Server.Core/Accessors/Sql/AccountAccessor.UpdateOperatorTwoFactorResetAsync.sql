UPDATE
    AdminUsers
SET
    TwoFactorEnabled = 0,
    AuthenticatorKey = NULL,
    RecoveryCodes = NULL,
    SecurityStamp = /*@ securityStamp */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId IS NULL
    AND Role = 'Operator'
    AND Id = /*@ id */''
