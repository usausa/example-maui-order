UPDATE
    Visits
SET
    Adults = /*@ adults */0,
    Children = /*@ children */0,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Open', 'Paying')
    AND Version = /*@ version */0
