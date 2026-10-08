SELECT
    L.Id,
    L.OrderId,
    L.VisitId,
    O.OrderNo,
    V.TableId,
    T.Name AS TableName,
    L.Name,
    L.Quantity,
    L.Status,
    L.ReleasedAt,
    L.ReadyAt
FROM
    OrderLines L
    JOIN Orders O ON O.TenantId = L.TenantId AND O.Id = L.OrderId
    JOIN Visits V ON V.TenantId = L.TenantId AND V.Id = L.VisitId
    JOIN DiningTables T ON T.TenantId = V.TenantId AND T.Id = V.TableId
WHERE
    L.TenantId = /*@ tenantId */''
    AND L.StoreId = /*@ storeId */''
    AND L.Status = /*@ status */'Ready'
    AND V.Status IN ('Open', 'Paying')
ORDER BY
    COALESCE(L.ReadyAt, L.ReleasedAt),
    O.OrderNo,
    L.LineNo
