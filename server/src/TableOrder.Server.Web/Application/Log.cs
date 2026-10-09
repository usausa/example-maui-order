namespace TableOrder.Server.Web.Application;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Service start.")]
    public static partial void InfoServiceStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Runtime: os=[{osDescription}], framework=[{frameworkDescription}], rid=[{runtimeIdentifier}]")]
    public static partial void InfoServiceSettingsRuntime(this ILogger logger, string osDescription, string frameworkDescription, string runtimeIdentifier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Environment: version=[{version}], directory=[{directory}]")]
    public static partial void InfoServiceSettingsEnvironment(this ILogger logger, Version? version, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "GCSettings: serverGC=[{isServerGC}], latencyMode=[{latencyMode}], largeObjectHeapCompactionMode=[{largeObjectHeapCompactionMode}]")]
    public static partial void InfoServiceSettingsGC(this ILogger logger, bool isServerGC, System.Runtime.GCLatencyMode latencyMode, System.Runtime.GCLargeObjectHeapCompactionMode largeObjectHeapCompactionMode);

    [LoggerMessage(Level = LogLevel.Information, Message = "ThreadPool: workerThreads=[{workerThreads}], completionPortThreads=[{completionPortThreads}]")]
    public static partial void InfoServiceSettingsThreadPool(this ILogger logger, int workerThreads, int completionPortThreads);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telemetry: otelEndPoint=[{otelEndPoint}]")]
    public static partial void InfoServiceSettingsTelemetry(this ILogger logger, string otelEndPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample data loaded.")]
    public static partial void InfoSampleDataLoaded(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample images copied. tenant=[{tenant}], count=[{count}]")]
    public static partial void InfoSampleImagesCopied(this ILogger logger, string tenant, int count);

    // Security

    [LoggerMessage(Level = LogLevel.Warning, Message = "Signing key is not configured. An ephemeral key is generated for development.")]
    public static partial void WarnEphemeralSigningKey(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Revocation list refresh failed.")]
    public static partial void ErrorRevocationRefresh(this ILogger logger, Exception ex);

    // Admin

    [LoggerMessage(Level = LogLevel.Information, Message = "Initial operator created. email=[{email}]")]
    public static partial void InfoInitialOperatorCreated(this ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No operator exists. Set Admin:InitialOperatorEmail and Admin:InitialOperatorPassword to create the first operator.")]
    public static partial void WarnNoOperator(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Initial operator password does not meet the password policy.")]
    public static partial void WarnInitialOperatorPasswordInvalid(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authenticator key could not be read. user=[{userId}]")]
    public static partial void WarnAuthenticatorKeyUnreadable(this ILogger logger, Exception ex, Guid userId);

    // Request

    [LoggerMessage(Level = LogLevel.Warning, Message = "Long execution. method=[{method}], route=[{route}], elapsed=[{elapsed}]")]
    public static partial void WarnLongExecution(this ILogger logger, string method, string route, long elapsed);

    // Event

    [LoggerMessage(Level = LogLevel.Error, Message = "Event dispatch failed.")]
    public static partial void ErrorEventDispatch(this ILogger logger, Exception ex);

    // Cleanup

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired events deleted. count=[{count}]")]
    public static partial void InfoEventCleanup(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired pairing codes and enrollment tokens deleted. count=[{count}]")]
    public static partial void InfoEnrollmentCleanup(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cleanup failed.")]
    public static partial void ErrorCleanup(this ILogger logger, Exception ex);

    // Simulation

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulation start.")]
    public static partial void InfoSimulationStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Simulation failed.")]
    public static partial void ErrorSimulation(this ILogger logger, Exception ex);

    // Error

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception.")]
    public static partial void ErrorUnhandledException(this ILogger logger, Exception ex);
}
