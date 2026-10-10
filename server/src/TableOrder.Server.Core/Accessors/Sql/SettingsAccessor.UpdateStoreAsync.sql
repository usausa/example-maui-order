UPDATE
    Stores
SET
    Languages = /*@ languages */'',
    PaymentMethods = /*@ paymentMethods */'',
    ElectronicReceipt = /*@ electronicReceipt */0,
    Features = /*@ features */'',
    StaffPinHash = COALESCE(/*@ staffPinHash */NULL, StaffPinHash),
    SettingsVersion = SettingsVersion + 1,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ storeId */''
    AND Version = /*@ version */0
