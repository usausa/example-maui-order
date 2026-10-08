UPDATE
    Payments
SET
    Status = 'Failed',
    Provider = COALESCE(/*@ provider */NULL, Provider),
    ProviderReference = COALESCE(/*@ providerReference */NULL, ProviderReference),
    FailureReason = /*@ reason */NULL,
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Pending'
