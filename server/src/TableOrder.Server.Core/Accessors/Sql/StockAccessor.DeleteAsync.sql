DELETE FROM
    Stocks
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND TargetId = /*@ targetId */''
