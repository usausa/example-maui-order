UPDATE
    Stores
SET
    OrderingPaused = /*@ paused */1,
    PausedMessage = /*@ message */NULL,
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
