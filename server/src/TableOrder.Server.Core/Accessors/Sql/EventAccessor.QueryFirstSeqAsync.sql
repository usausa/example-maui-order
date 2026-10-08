SELECT
    COALESCE(MIN(Seq), 0)
FROM
    Events
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
