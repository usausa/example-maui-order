UPDATE
    OrderLines
SET
    Status = 'Cooking',
    StartedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Ordered'
