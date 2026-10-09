SELECT
    COUNT(*)
FROM
    Devices
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND IsActive = 1
