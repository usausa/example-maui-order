SELECT
    *
FROM
    DeviceStations
WHERE
    TenantId = /*@ tenantId */''
    AND DeviceId = /*@ deviceId */''
ORDER BY
    StationId
