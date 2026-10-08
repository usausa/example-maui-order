SELECT
    COALESCE(MAX(LineNo), 0) + 1
FROM
    OrderLines
WHERE
    TenantId = /*@ tenantId */''
    AND OrderId = /*@ orderId */''
