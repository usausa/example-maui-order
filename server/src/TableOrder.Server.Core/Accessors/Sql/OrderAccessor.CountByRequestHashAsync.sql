SELECT
    COUNT(*)
FROM
    Orders
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND RequestHash = /*@ requestHash */NULL
