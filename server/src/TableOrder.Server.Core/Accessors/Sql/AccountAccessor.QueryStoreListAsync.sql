SELECT
    *
FROM
    AdminUserStores
WHERE
    TenantId = /*@ tenantId */''
    AND UserId = /*@ userId */''
ORDER BY
    StoreId
