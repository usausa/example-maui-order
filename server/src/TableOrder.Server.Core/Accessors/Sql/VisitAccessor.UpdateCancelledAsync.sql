UPDATE
    Visits
SET
    Status = 'Cancelled',
    ClosedBy = 'Hall',
    ClosedAt = /*@ now */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Open'
    AND Version = /*@ version */0
