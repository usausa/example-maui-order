SELECT
    T.Id,
    T.Name,
    T.Area,
    T.Capacity,
    T.SortOrder,
    T.IsActive,
    T.Version,
    (SELECT COUNT(*) FROM Devices D WHERE D.TenantId = T.TenantId AND D.TableId = T.Id AND D.IsActive = 1) AS DeviceCount,
    EXISTS (SELECT 1 FROM Visits V WHERE V.TenantId = T.TenantId AND V.TableId = T.Id AND V.Status IN ('Open', 'Paying')) AS HasOpenVisit
FROM
    DiningTables T
WHERE
    T.TenantId = /*@ tenantId */''
    AND T.StoreId = /*@ storeId */''
ORDER BY
    T.SortOrder,
    T.Name
