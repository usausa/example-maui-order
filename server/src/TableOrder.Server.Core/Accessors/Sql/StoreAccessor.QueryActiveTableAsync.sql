SELECT
    *
FROM
    DiningTables
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
    AND IsActive = 1
