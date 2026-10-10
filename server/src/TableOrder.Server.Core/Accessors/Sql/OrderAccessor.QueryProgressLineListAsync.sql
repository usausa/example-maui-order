SELECT
    L.*
FROM
    OrderLines L
WHERE
    L.TenantId = /*@ tenantId */''
    AND L.StoreId = /*@ storeId */''
    AND L.Status IN ('Ordered', 'Cooking', 'Ready')
    AND L.ServedBy = 'Staff'
ORDER BY
    L.ReleasedAt
