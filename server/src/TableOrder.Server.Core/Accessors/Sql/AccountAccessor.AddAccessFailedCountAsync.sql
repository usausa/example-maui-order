UPDATE
    AdminUsers
SET
    AccessFailedCount = AccessFailedCount + 1
WHERE
    Id = /*@ id */''
    AND IsActive = 1
RETURNING
    AccessFailedCount
