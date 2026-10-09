INSERT INTO
    AdminUsers (Id, TenantId, Role, Email, NormalizedEmail, Name, PasswordHash, MustChangePassword, SecurityStamp, AccessFailedCount, LockoutEnd, TwoFactorEnabled, AuthenticatorKey, RecoveryCodes, LastSignInAt, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    (/*@ id */'', /*@ tenantId */NULL, /*@ role */'Operator', /*@ email */'', /*@ normalizedEmail */'', /*@ name */'', /*@ passwordHash */'', /*@ mustChangePassword */0, /*@ securityStamp */'', 0, NULL, 0, NULL, NULL, NULL, 1, /*@ now */'', /*@ now */'', 1)
