UPDATE
    OrderLines
SET
    Status = 'Cooking',
    ReadyAt = NULL
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Ready'
