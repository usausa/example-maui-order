SELECT
    *
FROM
    CallReasons
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
ORDER BY
    SortOrder
