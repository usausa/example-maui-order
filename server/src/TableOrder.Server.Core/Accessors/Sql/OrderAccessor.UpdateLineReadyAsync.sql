UPDATE
    OrderLines
SET
    Status = 'Ready',
    StartedAt = COALESCE(StartedAt, /*@ now */''),
    ReadyAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Ordered', 'Cooking')
