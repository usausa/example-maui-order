SELECT
    *
FROM
    Payments
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
ORDER BY
    CreatedAt
