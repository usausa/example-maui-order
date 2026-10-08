SELECT
    T.*
FROM
    DiningTables T
WHERE
    T.TenantId = /*@ tenantId */''
    AND T.StoreId = /*@ storeId */''
    AND T.IsActive = 1
    AND T.Capacity >= /*@ guests */0
    AND NOT EXISTS (SELECT 1 FROM Visits V WHERE V.TenantId = T.TenantId AND V.TableId = T.Id AND V.Status IN ('Open', 'Paying'))
ORDER BY
    T.Capacity,
    T.SortOrder
LIMIT
    1
