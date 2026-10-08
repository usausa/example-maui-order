INSERT INTO
    OrderLines (TenantId, Id, StoreId, OrderId, VisitId, LineNo, ItemId, ItemCode, Name, Tags, Quantity, UnitPrice, Amount, TaxRate, Timing, Status, StationId, ServedBy, TicketId, ReleasedAt, StartedAt, ReadyAt, ServedAt, ServedStaffId, CancelledAt, CancelReason, CancelStaffId, SplitFromLineId)
VALUES
    (/*@ tenantId */'', /*@ id */'', /*@ storeId */'', /*@ orderId */'', /*@ visitId */'', /*@ lineNo */0, /*@ itemId */'', /*@ itemCode */'', /*@ name */'', /*@ tags */'', /*@ quantity */0, /*@ unitPrice */0, /*@ amount */0, /*@ taxRate */0, /*@ timing */'Now', /*@ status */'Ordered', /*@ stationId */NULL, /*@ servedBy */'Staff', /*@ ticketId */NULL, /*@ releasedAt */NULL, NULL, /*@ readyAt */NULL, /*@ servedAt */NULL, NULL, NULL, NULL, NULL, NULL)
