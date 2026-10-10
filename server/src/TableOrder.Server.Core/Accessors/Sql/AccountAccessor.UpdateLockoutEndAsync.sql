UPDATE
    AdminUsers
SET
    LockoutEnd = /*@ lockoutEnd */NULL
WHERE
    Id = /*@ id */''
    AND IsActive = 1
