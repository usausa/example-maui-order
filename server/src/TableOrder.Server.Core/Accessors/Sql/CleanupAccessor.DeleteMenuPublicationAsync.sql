DELETE FROM
    MenuPublications
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Id NOT IN (
        SELECT
            MenuPublicationId
        FROM
            Stores
        WHERE
            TenantId = /*@ tenantId */''
            AND Id = /*@ storeId */''
            AND MenuPublicationId IS NOT NULL
    )
    AND Id NOT IN (
        SELECT
            Id
        FROM
            MenuPublications
        WHERE
            TenantId = /*@ tenantId */''
            AND StoreId = /*@ storeId */''
        ORDER BY
            PublishedAt DESC,
            Id DESC
        LIMIT
            /*@ kept */0
    )
