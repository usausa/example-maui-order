UPDATE
    OrderLines
SET
    Status = 'Served',
    ServedAt = /*@ now */'',
    ServedStaffId = /*@ staffId */NULL
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Ordered', 'Cooking', 'Ready')
