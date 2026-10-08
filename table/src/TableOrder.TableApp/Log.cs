namespace TableOrder.TableApp;

using TableOrder.TableApp.Components;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

    // Device

    [LoggerMessage(Level = LogLevel.Information, Message = "Device registered. deviceId=[{deviceId}], storeId=[{storeId}]")]
    public static partial void InfoDeviceRegistered(this ILogger logger, Guid deviceId, Guid storeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device registration failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnDeviceRegistrationFailed(this ILogger logger, ApiStatus status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device denied. reason=[{reason}]")]
    public static partial void WarnDeviceDenied(this ILogger logger, DeviceDenial reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Api end point changed. apiEndPoint=[{apiEndPoint}]")]
    public static partial void InfoEndPointChanged(this ILogger logger, string apiEndPoint);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Status report failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void DebugStatusReportFailed(this ILogger logger, ApiStatus status, string? errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Status report stopped.")]
    public static partial void WarnStatusReportStopped(this ILogger logger, Exception exception);

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Battery info changed. level=[{chargeLevel}], state=[{state}], source=[{powerSource}]")]
    public static partial void DebugBatteryState(this ILogger logger, double chargeLevel, BatteryState state, BatteryPowerSource powerSource);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connectivity changed. profile=[{profile}], access=[{access}]")]
    public static partial void DebugConnectivityState(this ILogger logger, NetworkProfile profile, NetworkAccess access);

    // Kiosk

    [LoggerMessage(Level = LogLevel.Information, Message = "Kiosk mode. mode=[{mode}]")]
    public static partial void InfoKioskMode(this ILogger logger, KioskMode mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kiosk released by staff.")]
    public static partial void InfoKioskReleased(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kiosk restored.")]
    public static partial void InfoKioskRestored(this ILogger logger);

    // Managed configuration

    [LoggerMessage(Level = LogLevel.Information, Message = "Managed configuration. apiEndPoint=[{apiEndPoint}], enrollmentToken=[{enrollmentToken}]")]
    public static partial void InfoManagedConfiguration(this ILogger logger, string apiEndPoint, bool enrollmentToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Managed configuration value is invalid. key=[{key}]")]
    public static partial void WarnManagedConfigurationInvalid(this ILogger logger, string key);

    // Order

    [LoggerMessage(Level = LogLevel.Warning, Message = "Api failed. operation=[{operation}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, string operation, ApiStatus status, string? errorCode);

    // Event

    [LoggerMessage(Level = LogLevel.Debug, Message = "Event received. type=[{type}], seq=[{seq}], occurredAt=[{occurredAt}]")]
    public static partial void DebugEventReceived(this ILogger logger, string type, long seq, DateTimeOffset occurredAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event delivery failed.")]
    public static partial void WarnEventDeliveryFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Events expired. Restart to reload the current state.")]
    public static partial void WarnEventsExpired(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Device updated by the admin console. Restart to reload the token and settings.")]
    public static partial void InfoDeviceUpdated(this ILogger logger);

    // Popup

    [LoggerMessage(Level = LogLevel.Warning, Message = "Popup close failed.")]
    public static partial void WarnPopupCloseFailed(this ILogger logger, Exception exception);

    // Navigation

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unhandled navigation error.")]
    public static partial void WarnUnhandledNavigationError(this ILogger logger, Exception exception);

#if DEBUG
    [LoggerMessage(Level = LogLevel.Warning, Message = "Leak suspected. target=[{target}]")]
    public static partial void WarnLeakSuspected(this ILogger logger, string target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closed object collected. target=[{target}]")]
    public static partial void DebugClosedObjectCollected(this ILogger logger, string target);
#endif
}
