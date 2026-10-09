UPDATE
    DiningTables
SET
    SortOrder = /*@ sortOrder */0,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
