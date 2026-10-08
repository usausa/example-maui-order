INSERT INTO
    OrderLines (TenantId, Id, StoreId, OrderId, VisitId, LineNo, ItemId, ItemCode, Name, Tags, Quantity, UnitPrice, Amount, TaxRate, Timing, Status, StationId, ServedBy, TicketId, ReleasedAt, StartedAt, ReadyAt, ServedAt, ServedStaffId, CancelledAt, CancelReason, CancelStaffId, SplitFromLineId)
SELECT
    TenantId, /*@ id */'', StoreId, OrderId, VisitId, /*@ lineNo */0, ItemId, ItemCode, Name, Tags, /*@ quantity */0, UnitPrice, UnitPrice * /*@ quantity */0, TaxRate, Timing, 'Cancelled', StationId, ServedBy, TicketId, ReleasedAt, StartedAt, ReadyAt, NULL, NULL, /*@ now */'', /*@ reason */NULL, /*@ staffId */NULL, Id
FROM
    OrderLines
WHERE
    TenantId = /*@ tenantId */''
    AND Id = /*@ fromId */''
