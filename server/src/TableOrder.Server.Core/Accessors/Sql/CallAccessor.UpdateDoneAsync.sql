UPDATE
    Calls
SET
    Status = 'Done',
    AcknowledgedAt = COALESCE(AcknowledgedAt, /*@ now */''),
    DoneAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Open', 'Acknowledged')
