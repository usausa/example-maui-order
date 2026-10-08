SELECT
    *
FROM
    Payments
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
