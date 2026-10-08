UPDATE
    Devices
SET
    IsActive = 0,
    RevokedAt = /*@ now */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
    AND IsActive = 1
    AND Version = /*@ version */0
