SELECT
    Id,
    BrandName,
    LogoImageName,
    Theme,
    Version
FROM
    Tenants
WHERE
    Id = /*@ tenantId */''
