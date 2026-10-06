namespace TableOrder.Terminal.Table;

using TableOrder.Terminal.Table.Components;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup failed. status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnStartupFailed(this ILogger logger, ApiStatus status, string? errorCode);

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

    // Order

    [LoggerMessage(Level = LogLevel.Warning, Message = "Api failed. operation=[{operation}], status=[{status}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, string operation, ApiStatus status, string? errorCode);

    // Event

    [LoggerMessage(Level = LogLevel.Debug, Message = "Event received. type=[{type}], seq=[{seq}], occurredAt=[{occurredAt}]")]
    public static partial void DebugEventReceived(this ILogger logger, string type, long seq, DateTimeOffset occurredAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event delivery failed.")]
    public static partial void WarnEventDeliveryFailed(this ILogger logger, Exception exception);

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
