UPDATE
    Payments
SET
    Status = 'Cancelled',
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Pending'
