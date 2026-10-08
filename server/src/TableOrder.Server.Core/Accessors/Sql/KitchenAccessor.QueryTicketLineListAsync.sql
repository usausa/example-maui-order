SELECT
    *
FROM
    OrderLines
WHERE
    TenantId = /*@ tenantId */''
    AND TicketId = /*@ ticketId */''
ORDER BY
    LineNo
