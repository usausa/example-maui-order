UPDATE
    AdminUsers
SET
    IsActive = /*@ isActive */1,
    SecurityStamp = /*@ securityStamp */'',
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Version = /*@ version */0
