UPDATE
    OrderLines
SET
    Status = 'Cancelled',
    CancelledAt = /*@ now */'',
    CancelReason = /*@ reason */NULL,
    CancelStaffId = /*@ staffId */NULL
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Held', 'Ordered', 'Cooking', 'Ready')
