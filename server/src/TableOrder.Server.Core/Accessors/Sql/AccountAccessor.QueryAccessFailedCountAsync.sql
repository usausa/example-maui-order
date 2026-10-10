SELECT
    COALESCE(MAX(AccessFailedCount), 0)
FROM
    AdminUsers
WHERE
    Id = /*@ id */''
