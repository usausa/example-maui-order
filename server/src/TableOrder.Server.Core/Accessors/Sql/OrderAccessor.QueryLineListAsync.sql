SELECT
    L.*
FROM
    OrderLines L
    JOIN Orders O ON O.TenantId = L.TenantId AND O.Id = L.OrderId
WHERE
    L.TenantId = /*@ tenantId */''
    AND L.VisitId = /*@ visitId */''
ORDER BY
    O.OrderNo,
    L.LineNo
