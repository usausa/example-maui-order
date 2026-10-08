UPDATE
    Payments
SET
    Status = 'Completed',
    Provider = COALESCE(/*@ provider */NULL, Provider),
    ProviderReference = COALESCE(/*@ providerReference */NULL, ProviderReference),
    CompletedAt = /*@ now */'',
    UpdatedAt = /*@ now */''
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status = 'Pending'
