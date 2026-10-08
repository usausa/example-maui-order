SELECT
    P.*
FROM
    OrderLineOptions P
    JOIN OrderLines L ON L.TenantId = P.TenantId AND L.Id = P.LineId
WHERE
    P.TenantId = /*@ tenantId */''
    AND L.TicketId = /*@ ticketId */''
ORDER BY
    P.LineId,
    P.SortOrder
