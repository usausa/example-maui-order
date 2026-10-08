INSERT INTO
    Payments (TenantId, Id, StoreId, VisitId, Method, Amount, Status, QrCode, ExpiresAt, Provider, ProviderReference, FailureReason, DeviceId, CreatedAt, UpdatedAt, CompletedAt)
VALUES
    (/*@ tenantId */'', /*@ id */'', /*@ storeId */'', /*@ visitId */'', /*@ method */'QrCode', /*@ amount */0, 'Pending', /*@ qrCode */NULL, /*@ expiresAt */NULL, /*@ provider */NULL, /*@ providerReference */NULL, NULL, /*@ deviceId */NULL, /*@ now */'', /*@ now */'', NULL)
