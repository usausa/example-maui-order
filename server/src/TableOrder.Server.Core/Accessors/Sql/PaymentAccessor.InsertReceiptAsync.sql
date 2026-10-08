INSERT INTO
    Receipts (TenantId, VisitId, Token, IssuedAt)
VALUES
    (/*@ tenantId */'', /*@ visitId */'', /*@ token */'', /*@ now */'')
ON CONFLICT (TenantId, VisitId) DO NOTHING
