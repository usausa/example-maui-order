SELECT
    P.*
FROM
    OrderLineOptions P
    JOIN OrderLines L ON L.TenantId = P.TenantId AND L.Id = P.LineId
    JOIN KitchenTickets K ON K.TenantId = L.TenantId AND K.Id = L.TicketId
WHERE
    P.TenantId = /*@ tenantId */''
    AND K.StoreId = /*@ storeId */''
    AND K.Status = 'Open'
ORDER BY
    P.LineId,
    P.SortOrder
