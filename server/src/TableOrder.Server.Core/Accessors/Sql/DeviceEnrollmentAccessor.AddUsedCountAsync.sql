UPDATE
    DeviceEnrollments
SET
    UsedCount = UsedCount + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND UsedCount < MaxUses
    AND RevokedAt IS NULL
    AND ExpiresAt > /*@ now */''
