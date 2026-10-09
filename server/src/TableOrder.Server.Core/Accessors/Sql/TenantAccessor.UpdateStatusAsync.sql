UPDATE
    Tenants
SET
    Status = /*@ status */'Active',
    SuspendedAt = /*@ suspendedAt */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status <> 'Closed'
    AND Version = /*@ version */0
