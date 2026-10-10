SELECT
    K.*,
    V.TableId,
    T.Name AS TableName,
    O.OrderNo
FROM
    KitchenTickets K
    JOIN Orders O ON O.TenantId = K.TenantId AND O.Id = K.OrderId
    JOIN Visits V ON V.TenantId = K.TenantId AND V.Id = K.VisitId
    JOIN DiningTables T ON T.TenantId = V.TenantId AND T.Id = V.TableId
WHERE
    K.TenantId = /*@ tenantId */''
    AND K.VisitId = /*@ visitId */''
    AND K.Status = 'Open'
ORDER BY
    K.CreatedAt
