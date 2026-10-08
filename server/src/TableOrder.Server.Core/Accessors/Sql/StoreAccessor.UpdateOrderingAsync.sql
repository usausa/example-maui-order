UPDATE
    Stores
SET
    OrderingPaused = /*@ paused */0,
    PausedMessage = /*@ message */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
