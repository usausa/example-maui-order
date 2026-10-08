SELECT
    T.Id,
    T.Name,
    T.Area,
    T.Capacity,
    T.SortOrder,
    V.Id AS VisitId,
    V.Adults,
    V.Children,
    V.Status AS VisitStatus,
    V.OpenedAt,
    V.Version AS VisitVersion,
    (SELECT MAX(O.OrderedAt) FROM Orders O WHERE O.TenantId = V.TenantId AND O.VisitId = V.Id) AS LastOrderedAt,
    (SELECT COUNT(*) FROM OrderLines L WHERE L.TenantId = V.TenantId AND L.VisitId = V.Id AND L.Status IN ('Ordered', 'Cooking', 'Ready')) AS UnservedCount,
    (SELECT COUNT(*) FROM Calls C WHERE C.TenantId = V.TenantId AND C.VisitId = V.Id AND C.Status <> 'Done') AS OpenCallCount
FROM
    DiningTables T
    LEFT JOIN Visits V ON V.TenantId = T.TenantId AND V.TableId = T.Id AND V.Status IN ('Open', 'Paying')
WHERE
    T.TenantId = /*@ tenantId */''
    AND T.StoreId = /*@ storeId */''
    AND T.IsActive = 1
ORDER BY
    T.SortOrder
