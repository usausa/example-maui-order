DELETE FROM
    Receipts
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId IN (
        SELECT
            Id
        FROM
            Visits
        WHERE
            TenantId = /*@ tenantId */''
            AND StoreId = /*@ storeId */''
            AND Status IN ('Closed', 'Cancelled')
            AND BusinessDate < /*@ before */''
        ORDER BY
            BusinessDate,
            Id
        LIMIT
            /*@ limit */0
    )
