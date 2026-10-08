INSERT INTO
    EventSequences (TenantId, StoreId, LastSeq)
VALUES
    (/*@ tenantId */'', /*@ storeId */'', 0)
ON CONFLICT (TenantId, StoreId) DO UPDATE SET
    LastSeq = LastSeq
