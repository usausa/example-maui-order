UPDATE
    Tenants
SET
    BrandName = /*@ brandName */'',
    LogoImageName = /*@ logoImageName */NULL,
    Theme = /*@ theme */NULL,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    Id = /*@ tenantId */''
    AND Version = /*@ version */0
