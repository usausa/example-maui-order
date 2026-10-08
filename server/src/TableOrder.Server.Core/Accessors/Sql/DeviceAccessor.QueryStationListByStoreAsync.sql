SELECT
    S.TenantId,
    S.DeviceId,
    S.StationId
FROM
    DeviceStations S
    JOIN Devices D ON D.TenantId = S.TenantId AND D.Id = S.DeviceId
WHERE
    S.TenantId = /*@ tenantId */''
    AND D.StoreId = /*@ storeId */''
ORDER BY
    S.DeviceId,
    S.StationId
