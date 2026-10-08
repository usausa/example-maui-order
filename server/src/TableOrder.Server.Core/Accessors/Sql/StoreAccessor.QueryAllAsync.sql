SELECT
    *
FROM
    Stores
WHERE
    TenantId = /*@ tenantId */''
ORDER BY
    Code
