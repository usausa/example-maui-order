SELECT
    *
FROM
    AdminUserStores
WHERE
    TenantId = /*@ tenantId */''
ORDER BY
    UserId,
    StoreId
