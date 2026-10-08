INSERT INTO
    Stocks (TenantId, StoreId, TargetId, TargetKind, Status, Remaining, UpdatedAt)
VALUES
    (/*@ tenantId */'', /*@ storeId */'', /*@ targetId */'', /*@ targetKind */'Item', /*@ status */'SoldOut', /*@ remaining */NULL, /*@ now */'')
ON CONFLICT (TenantId, StoreId, TargetId) DO UPDATE SET
    TargetKind = excluded.TargetKind,
    Status = excluded.Status,
    Remaining = excluded.Remaining,
    UpdatedAt = excluded.UpdatedAt
