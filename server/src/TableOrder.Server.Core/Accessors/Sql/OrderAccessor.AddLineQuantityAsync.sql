UPDATE
    OrderLines
SET
    Quantity = Quantity + /*@ quantity */0,
    Amount = UnitPrice * (Quantity + /*@ quantity */0)
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ id */''
    AND Status IN ('Held', 'Ordered', 'Cooking', 'Ready')
    AND Quantity + /*@ quantity */0 >= 1
