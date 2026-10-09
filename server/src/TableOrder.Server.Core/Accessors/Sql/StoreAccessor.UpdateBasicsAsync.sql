UPDATE
    Stores
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    TimeZone = /*@ timeZone */'',
    OpenTime = /*@ openTime */'',
    CloseTime = /*@ closeTime */'',
    LastOrderTime = /*@ lastOrderTime */NULL,
    TaxRounding = /*@ taxRounding */'Floor',
    MaxQuantityPerLine = /*@ maxQuantityPerLine */0,
    MaxLinesPerOrder = /*@ maxLinesPerOrder */0,
    SettingsVersion = SettingsVersion + 1,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Version = /*@ version */0
