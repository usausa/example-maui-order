INSERT INTO
    Receipts (TenantId, VisitId, StoreId, Token, IssuerName, InvoiceRegistrationNumber, IssuedAt)
VALUES
    (/*@ tenantId */'', /*@ visitId */'', /*@ storeId */'', /*@ token */'', /*@ issuerName */NULL, /*@ invoiceRegistrationNumber */NULL, /*@ now */'')
ON CONFLICT (TenantId, VisitId) DO NOTHING
