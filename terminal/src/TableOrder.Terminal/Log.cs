namespace TableOrder.Terminal;

using TableOrder.Terminal.Components;

internal static partial class Log
{
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

#if DEBUG
    // Navigation

    [LoggerMessage(Level = LogLevel.Warning, Message = "Leak suspected. target=[{target}]")]
    public static partial void WarnLeakSuspected(this ILogger logger, string target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closed object collected. target=[{target}]")]
    public static partial void DebugClosedObjectCollected(this ILogger logger, string target);
#endif
}
