SELECT
    V.*,
    T.Name AS TableName,
    (SELECT COALESCE(SUM(L.Amount), 0) FROM OrderLines L WHERE L.TenantId = V.TenantId AND L.VisitId = V.Id AND L.Status <> 'Cancelled') AS OrderTotal
FROM
    Visits V
    JOIN DiningTables T ON T.TenantId = V.TenantId AND T.Id = V.TableId
WHERE
    V.TenantId = /*@ tenantId */''
    AND V.StoreId = /*@ storeId */''
    AND V.Id = /*@ id */''
