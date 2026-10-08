SELECT
    L.TenantId,
    L.StoreId
FROM
    OrderLines L
    JOIN Visits V ON V.TenantId = L.TenantId AND V.Id = L.VisitId
WHERE
    L.Status IN ('Ordered', 'Cooking', 'Ready')
    AND L.ServedBy = 'Staff'
    AND V.Status IN ('Open', 'Paying')
UNION
SELECT
    TenantId,
    StoreId
FROM
    Calls
WHERE
    Status <> 'Done'
UNION
SELECT
    TenantId,
    StoreId
FROM
    Payments
WHERE
    Status = 'Pending'
