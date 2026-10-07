INSERT INTO
    DeviceStatuses (TenantId, DeviceId, AppVersion, BatteryLevel, IsCharging, LastSeenAt)
VALUES
    (/*@ tenantId */'', /*@ deviceId */'', /*@ appVersion */NULL, /*@ batteryLevel */NULL, /*@ isCharging */NULL, /*@ lastSeenAt */'')
ON CONFLICT (TenantId, DeviceId) DO UPDATE SET
    AppVersion = COALESCE(excluded.AppVersion, AppVersion),
    BatteryLevel = excluded.BatteryLevel,
    IsCharging = excluded.IsCharging,
    LastSeenAt = excluded.LastSeenAt
