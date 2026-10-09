UPDATE
    DiningTables
SET
    Name = /*@ name */'',
    Area = /*@ area */NULL,
    Capacity = /*@ capacity */0,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id = /*@ id */''
    AND Version = /*@ version */0
