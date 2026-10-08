SELECT
    *
FROM
    Receipts
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
