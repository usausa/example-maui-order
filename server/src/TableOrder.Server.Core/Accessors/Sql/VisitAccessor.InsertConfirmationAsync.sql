INSERT INTO
    VisitConfirmations (TenantId, VisitId, RuleId, DeviceId, ConfirmedAt)
VALUES
    (/*@ tenantId */'', /*@ visitId */'', /*@ ruleId */'', /*@ deviceId */NULL, /*@ now */'')
ON CONFLICT (TenantId, VisitId, RuleId) DO NOTHING
