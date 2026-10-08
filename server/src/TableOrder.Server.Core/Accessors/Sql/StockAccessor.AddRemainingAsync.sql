UPDATE
    Stocks
SET
    Status = CASE WHEN Remaining + /*@ quantity */0 > 0 THEN 'Limited' ELSE 'SoldOut' END,
    Remaining = CASE WHEN Remaining + /*@ quantity */0 > 0 THEN Remaining + /*@ quantity */0 ELSE NULL END,
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND TargetId = /*@ targetId */''
    AND Status = 'Limited'
    AND Remaining + /*@ quantity */0 >= 0
