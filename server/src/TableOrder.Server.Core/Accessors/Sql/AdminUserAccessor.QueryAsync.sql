SELECT
    Id,
    TenantId,
    Role,
    Email,
    Name,
    MustChangePassword,
    TwoFactorEnabled,
    LockoutEnd,
    LastSignInAt,
    IsActive,
    Version
FROM
    AdminUsers
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
