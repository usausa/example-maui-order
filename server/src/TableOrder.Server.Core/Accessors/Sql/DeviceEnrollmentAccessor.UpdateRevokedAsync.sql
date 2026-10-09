UPDATE
    DeviceEnrollments
SET
    RevokedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
    AND Method = 'EnrollmentToken'
    AND RevokedAt IS NULL
