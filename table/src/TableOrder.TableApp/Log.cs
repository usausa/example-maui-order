namespace TableOrder.TableApp;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

    // Theme

    [LoggerMessage(Level = LogLevel.Warning, Message = "Theme color ignored. role=[{role}], color=[{color}]")]
    public static partial void WarnThemeColorIgnored(this ILogger logger, string role, string color);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings changed. Restart to apply the chain and store settings. version=[{version}]")]
    public static partial void InfoSettingsChanged(this ILogger logger, int version);

    // Image

    [LoggerMessage(Level = LogLevel.Information, Message = "Images synced. saved=[{saved}], missing=[{missing}]")]
    public static partial void InfoImagesSynced(this ILogger logger, int saved, int missing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image download failed. name=[{name}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnImageFailed(this ILogger logger, string name, ApiStatus status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image save failed. name=[{name}]")]
    public static partial void WarnImageSaveFailed(this ILogger logger, string name, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image cleanup failed.")]
    public static partial void WarnImageCleanupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image sync stopped.")]
    public static partial void WarnImageSyncStopped(this ILogger logger, Exception exception);

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
