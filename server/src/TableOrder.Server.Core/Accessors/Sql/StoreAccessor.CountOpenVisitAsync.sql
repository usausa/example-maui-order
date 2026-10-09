SELECT
    COUNT(*)
FROM
    Visits
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Status IN ('Open', 'Paying')
