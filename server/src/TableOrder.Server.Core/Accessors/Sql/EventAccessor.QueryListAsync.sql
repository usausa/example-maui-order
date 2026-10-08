SELECT
    *
FROM
    Events
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Seq > /*@ after */0
ORDER BY
    Seq
LIMIT
    /*@ limit */0
