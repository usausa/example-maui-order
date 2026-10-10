SELECT
    L.TenantId,
    L.StoreId
FROM
    OrderLines L
WHERE
    L.Status IN ('Ordered', 'Cooking', 'Ready')
    AND L.ServedBy = 'Staff'
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
