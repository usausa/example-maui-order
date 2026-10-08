UPDATE
    EventSequences
SET
    LastSeq = LastSeq + 1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
RETURNING
    LastSeq
