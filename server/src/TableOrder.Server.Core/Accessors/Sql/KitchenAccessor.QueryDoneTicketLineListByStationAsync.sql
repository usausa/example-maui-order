SELECT
    L.*
FROM
    OrderLines L
WHERE
    L.TenantId = /*@ tenantId */''
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
            K.DoneAt DESC
        LIMIT
            /*@ limit */0
    )
ORDER BY
    L.LineNo
