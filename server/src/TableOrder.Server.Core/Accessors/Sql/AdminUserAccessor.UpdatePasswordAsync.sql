UPDATE
    AdminUsers
SET
    PasswordHash = /*@ passwordHash */'',
    MustChangePassword = 1,
    AccessFailedCount = 0,
    LockoutEnd = NULL,
    SecurityStamp = /*@ securityStamp */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
