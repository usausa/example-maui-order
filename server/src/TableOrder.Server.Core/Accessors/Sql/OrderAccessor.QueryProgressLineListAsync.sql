SELECT
    L.*
FROM
    OrderLines L
    JOIN Visits V ON V.TenantId = L.TenantId AND V.Id = L.VisitId
WHERE
    L.TenantId = /*@ tenantId */''
    AND L.StoreId = /*@ storeId */''
    AND L.Status IN ('Ordered', 'Cooking', 'Ready')
    AND L.ServedBy = 'Staff'
    AND V.Status IN ('Open', 'Paying')
ORDER BY
    L.ReleasedAt
