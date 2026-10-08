UPDATE
    KitchenTickets
SET
    Status = 'Done',
    DoneAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Open'
