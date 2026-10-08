UPDATE
    OrderLines
SET
    Status = /*@ status */'Ordered',
    TicketId = /*@ ticketId */NULL,
    ReleasedAt = /*@ now */'',
    ReadyAt = /*@ readyAt */NULL,
    ServedAt = /*@ servedAt */NULL
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Held'
