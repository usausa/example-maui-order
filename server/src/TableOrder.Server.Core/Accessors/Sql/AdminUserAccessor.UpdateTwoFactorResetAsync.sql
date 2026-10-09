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
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
