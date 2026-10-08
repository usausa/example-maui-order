SELECT
    COUNT(*)
FROM
    OrderLines
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
    AND Status <> 'Cancelled'
