UPDATE
    Stores
SET
    SettingsVersion = SettingsVersion + 1,
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ storeId */''
