INSERT INTO
    OrderLineOptions (TenantId, LineId, SortOrder, OptionGroupId, OptionId, Name, PriceDelta)
SELECT
    TenantId, /*@ lineId */'', SortOrder, OptionGroupId, OptionId, Name, PriceDelta
FROM
    OrderLineOptions
WHERE
    TenantId = /*@ tenantId */''
    AND LineId = /*@ fromLineId */''
