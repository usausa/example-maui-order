SELECT
    D.Id,
    D.Kind,
    D.Name,
    D.TableId,
    T.Name AS TableName,
    D.IsActive,
    D.RegisteredAt,
    D.RevokedAt,
    D.Version,
    S.AppVersion,
    S.BatteryLevel,
    S.IsCharging,
    S.LastSeenAt
FROM
    Devices D
    LEFT JOIN DiningTables T ON T.TenantId = D.TenantId AND T.Id = D.TableId
    LEFT JOIN DeviceStatuses S ON S.TenantId = D.TenantId AND S.DeviceId = D.Id
WHERE
    D.TenantId = /*@ tenantId */''
    AND D.StoreId = /*@ storeId */''
ORDER BY
    D.IsActive DESC,
    D.Kind,
    D.Name,
    D.RegisteredAt
