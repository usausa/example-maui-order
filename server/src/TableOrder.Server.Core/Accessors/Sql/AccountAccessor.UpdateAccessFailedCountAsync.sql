UPDATE
    AdminUsers
SET
    AccessFailedCount = /*@ accessFailedCount */0
WHERE
    Id = /*@ id */''
    AND IsActive = 1
