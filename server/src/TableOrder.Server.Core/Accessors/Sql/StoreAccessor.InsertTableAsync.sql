INSERT INTO
    DiningTables (TenantId, Id, StoreId, Name, Area, Capacity, SortOrder, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    (/*@ tenantId */'', /*@ id */'', /*@ storeId */'', /*@ name */'', /*@ area */NULL, /*@ capacity */0, (SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM DiningTables WHERE TenantId = /*@ tenantId */'' AND StoreId = /*@ storeId */''), 1, /*@ now */'', /*@ now */'', 1)
