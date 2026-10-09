UPDATE
    DiningTables
SET
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
    AND Version = /*@ version */0
