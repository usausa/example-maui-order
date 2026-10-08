SELECT
    COALESCE(MAX(OrderNo), 0) + 1
FROM
    Orders
WHERE
    TenantId = /*@ tenantId */''
    AND VisitId = /*@ visitId */''
