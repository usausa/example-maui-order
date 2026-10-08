UPDATE
    Calls
SET
    Status = 'Acknowledged',
    AcknowledgedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Open'
