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
    TenantId IS NULL
    AND Role = 'Operator'
ORDER BY
    Name,
    Email
