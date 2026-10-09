SELECT
    T.Id,
    T.Code,
    T.Name,
    T.BrandName,
    T.Status,
    T.SuspendedAt,
    (SELECT COUNT(*) FROM Stores S WHERE S.TenantId = T.Id AND S.IsActive = 1) AS StoreCount,
    T.CreatedAt,
    T.Version
FROM
    Tenants T
ORDER BY
    T.Code
