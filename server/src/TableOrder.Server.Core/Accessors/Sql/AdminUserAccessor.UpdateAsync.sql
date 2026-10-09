UPDATE
    AdminUsers
SET
    Role = /*@ role */'TenantAdmin',
    Name = /*@ name */'',
    SecurityStamp = /*@ securityStamp */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Version = /*@ version */0
