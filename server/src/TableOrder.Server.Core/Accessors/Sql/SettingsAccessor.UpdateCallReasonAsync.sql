UPDATE
    CallReasons
SET
    IsActive = /*@ isActive */0
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Code = /*@ code */''
