SELECT
    COALESCE(MAX(LastSeq), 0)
FROM
    EventSequences
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
