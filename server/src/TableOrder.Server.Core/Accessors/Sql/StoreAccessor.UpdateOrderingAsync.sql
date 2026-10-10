UPDATE
    Stores
SET
    OrderingPaused = /*@ paused */0,
    PausedMessage = /*@ message */NULL,
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
