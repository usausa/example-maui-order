SELECT
    *
FROM
    CallReasons
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND IsActive = 1
ORDER BY
    SortOrder
