SELECT
    *
FROM
    Stocks
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
ORDER BY
    TargetId
