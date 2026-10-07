SELECT
    P.*
FROM
    Stores S
    JOIN MenuPublications P ON P.TenantId = S.TenantId AND P.Id = S.MenuPublicationId
WHERE
    S.TenantId = /*@ tenantId */''
    AND S.Id = /*@ storeId */''
