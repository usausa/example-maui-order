SELECT
    P.*
FROM
    OrderLineOptions P
    JOIN OrderLines L ON L.TenantId = P.TenantId AND L.Id = P.LineId
    JOIN Visits V ON V.TenantId = L.TenantId AND V.Id = L.VisitId
WHERE
    P.TenantId = /*@ tenantId */''
    AND L.StoreId = /*@ storeId */''
    AND L.Status = /*@ status */'Ready'
    AND V.Status IN ('Open', 'Paying')
ORDER BY
    P.LineId,
    P.SortOrder
