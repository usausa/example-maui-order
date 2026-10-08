SELECT
    *
FROM
    Orders
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
