SELECT
    *
FROM
    VisitConfirmations
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
ORDER BY
    ConfirmedAt
