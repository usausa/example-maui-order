UPDATE
    CallReasons
SET
    IsActive = /*@ isActive */1
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Code = /*@ code */''
