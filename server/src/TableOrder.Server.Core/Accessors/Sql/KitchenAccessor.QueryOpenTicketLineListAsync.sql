SELECT
    L.*
FROM
    OrderLines L
    JOIN KitchenTickets K ON K.TenantId = L.TenantId AND K.Id = L.TicketId
WHERE
    L.TenantId = /*@ tenantId */''
    AND K.StoreId = /*@ storeId */''
    AND K.Status = 'Open'
ORDER BY
    L.LineNo
