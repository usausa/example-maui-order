UPDATE
    AdminUsers
SET
    Name = /*@ name */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId IS NULL
    AND Role = 'Operator'
    AND Id = /*@ id */''
    AND Version = /*@ version */0
