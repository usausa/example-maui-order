INSERT INTO
    Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, LastOrderTime, OrderingPaused, PausedMessage, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, Features, StaffPinHash, SettingsVersion, MenuPublicationId, IsActive, CreatedAt, UpdatedAt, Version)
VALUES
    (/*@ tenantId */'', /*@ id */'', /*@ code */'', /*@ name */'', /*@ timeZone */'', /*@ openTime */'', /*@ closeTime */'', /*@ lastOrderTime */NULL, 0, NULL, /*@ taxRounding */'Floor', /*@ maxQuantityPerLine */0, /*@ maxLinesPerOrder */0, /*@ languages */'', /*@ paymentMethods */'', /*@ electronicReceipt */0, /*@ features */'', /*@ staffPinHash */'', 1, /*@ menuPublicationId */'', 1, /*@ now */'', /*@ now */'', 1)
