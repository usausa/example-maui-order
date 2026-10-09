SELECT
    *
FROM
    DeviceEnrollments
WHERE
    TenantId = /*@ tenantId */''
    AND StoreId = /*@ storeId */''
    AND Method = 'EnrollmentToken'
ORDER BY
    CreatedAt DESC
