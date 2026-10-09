namespace TableOrder.KitchenApp;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}]")]
    public static partial void InfoApplicationStart(this ILogger logger, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restart. reason=[{reason}]")]
    public static partial void InfoRestart(this ILogger logger, string reason);

    // Device

    [LoggerMessage(Level = LogLevel.Information, Message = "Device registered. deviceId=[{deviceId}], storeId=[{storeId}]")]
    public static partial void InfoDeviceRegistered(this ILogger logger, Guid deviceId, Guid storeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device registration failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnDeviceRegistrationFailed(this ILogger logger, ApiStatus status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device key is not available.")]
    public static partial void WarnDeviceKeyUnavailable(this ILogger logger, Exception exception);

    // Event

    [LoggerMessage(Level = LogLevel.Warning, Message = "Api failed. operation=[{operation}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, string operation, ApiStatus status, string? errorCode);
}
