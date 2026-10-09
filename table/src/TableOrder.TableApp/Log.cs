namespace TableOrder.TableApp;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

    // Settings

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings changed. Restart to apply the chain and store settings. version=[{version}]")]
    public static partial void InfoSettingsChanged(this ILogger logger, int version);

    // State

    [LoggerMessage(Level = LogLevel.Debug, Message = "Screen state changed. state=[{on}]")]
    public static partial void DebugScreenStateChanged(this ILogger logger, bool on);

    // Order

    [LoggerMessage(Level = LogLevel.Warning, Message = "Api failed. operation=[{operation}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, string operation, ApiStatus status, string? errorCode);

    // Navigation

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unhandled navigation error.")]
    public static partial void WarnUnhandledNavigationError(this ILogger logger, Exception exception);
}
