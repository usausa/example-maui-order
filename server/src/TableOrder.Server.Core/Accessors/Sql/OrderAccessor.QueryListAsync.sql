SELECT
    *
FROM
    Orders
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
ORDER BY
    OrderNo
