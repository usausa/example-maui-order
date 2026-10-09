SELECT
    TenantId,
    Id
FROM
    Devices
WHERE
    IsActive = 0
    AND RevokedAt > /*@ since */''
