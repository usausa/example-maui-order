namespace TableOrder.Server.Web.Endpoints;

// Problem Details の errorCode (端末は文言ではなくこの値で扱いを決める)
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";

    public const string NotFound = "NOT_FOUND";

    public const string DeviceScope = "DEVICE_SCOPE";

    public const string DeviceRevoked = "DEVICE_REVOKED";

    public const string TenantSuspended = "TENANT_SUSPENDED";

    public const string PairingCodeInvalid = "PAIRING_CODE_INVALID";
}
