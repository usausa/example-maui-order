namespace TableOrder.Contract;

// API の失敗の errorCode (Problem Details に付ける)。端末は文言ではなくこの値で扱いを決める
public static class ErrorCodes
{
    // 400
    public const string ValidationError = "VALIDATION_ERROR";

    // 403
    public const string DeviceScope = "DEVICE_SCOPE";

    public const string DeviceRevoked = "DEVICE_REVOKED";

    public const string TenantSuspended = "TENANT_SUSPENDED";

    public const string VisitOpeningDisabled = "VISIT_OPENING_DISABLED";

    // 404
    public const string NotFound = "NOT_FOUND";

    // 409
    public const string DuplicateIdMismatch = "DUPLICATE_ID_MISMATCH";

    public const string VersionMismatch = "VERSION_MISMATCH";

    public const string TableOccupied = "TABLE_OCCUPIED";

    public const string NoVacantTable = "NO_VACANT_TABLE";

    // 410
    public const string EventsExpired = "EVENTS_EXPIRED";

    // 422
    public const string PairingCodeInvalid = "PAIRING_CODE_INVALID";

    public const string VisitNotOpen = "VISIT_NOT_OPEN";

    public const string CheckoutInProgress = "CHECKOUT_IN_PROGRESS";

    public const string OrderingPaused = "ORDERING_PAUSED";

    public const string LastOrderPassed = "LAST_ORDER_PASSED";

    public const string MenuChanged = "MENU_CHANGED";

    public const string ItemSoldOut = "ITEM_SOLD_OUT";

    public const string StockInsufficient = "STOCK_INSUFFICIENT";

    public const string OptionInvalid = "OPTION_INVALID";

    public const string QuantityExceeded = "QUANTITY_EXCEEDED";

    public const string ConfirmationRequired = "CONFIRMATION_REQUIRED";

    public const string LimitExceeded = "LIMIT_EXCEEDED";

    public const string LineStatusInvalid = "LINE_STATUS_INVALID";

    public const string VisitHasOrders = "VISIT_HAS_ORDERS";

    public const string BillChanged = "BILL_CHANGED";

    public const string PaymentAmountInvalid = "PAYMENT_AMOUNT_INVALID";

    public const string PaymentMethodUnavailable = "PAYMENT_METHOD_UNAVAILABLE";
}
