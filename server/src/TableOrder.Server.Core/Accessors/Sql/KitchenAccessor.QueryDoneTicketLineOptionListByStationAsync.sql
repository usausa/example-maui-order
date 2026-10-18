SELECT
    P.*
FROM
    OrderLineOptions P
    JOIN OrderLines L ON L.TenantId = P.TenantId AND L.Id = P.LineId
WHERE
    P.TenantId = /*@ tenantId */''
    AND L.TicketId IN (
        SELECT
            K.Id
        FROM
            KitchenTickets K
        WHERE
            K.TenantId = /*@ tenantId */''
            AND K.StoreId = /*@ storeId */''
            AND K.StationId = /*@ stationId */''
            AND K.Status = 'Done'
        ORDER BY
            K.DoneAt DESC,
            K.Id DESC
        LIMIT
            /*@ limit */20
    )
ORDER BY
    P.LineId,
    P.SortOrder
