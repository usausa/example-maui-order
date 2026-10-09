namespace TableOrder.HallApp;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

    // Event

    [LoggerMessage(Level = LogLevel.Warning, Message = "Api failed. operation=[{operation}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, string operation, ApiStatus status, string? errorCode);

    // Alert

    [LoggerMessage(Level = LogLevel.Warning, Message = "Call alert failed.")]
    public static partial void WarnCallAlertFailed(this ILogger logger, Exception exception);

    // Navigation

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unhandled navigation error.")]
    public static partial void WarnUnhandledNavigationError(this ILogger logger, Exception exception);
}
