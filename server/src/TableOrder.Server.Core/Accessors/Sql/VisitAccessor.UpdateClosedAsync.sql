UPDATE
    Visits
SET
    Status = 'Closed',
    ClosedBy = /*@ closedBy */'Hall',
    ClosedAt = /*@ now */'',
    ClosedStaffId = /*@ staffId */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Open', 'Paying')
    AND Version = /*@ version */0
