SELECT
    C.*,
    V.TableId,
    T.Name AS TableName
FROM
    Calls C
    JOIN Visits V ON V.TenantId = C.TenantId AND V.Id = C.VisitId
    JOIN DiningTables T ON T.TenantId = V.TenantId AND T.Id = V.TableId
WHERE
    C.TenantId = /*@ tenantId */''
    AND C.StoreId = /*@ storeId */''
    AND C.Id = /*@ id */''
