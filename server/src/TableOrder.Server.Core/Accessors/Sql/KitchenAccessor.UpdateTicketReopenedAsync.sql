UPDATE
    KitchenTickets
SET
    Status = 'Open',
    DoneAt = NULL
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Done'
