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

    // Security

    [LoggerMessage(Level = LogLevel.Warning, Message = "Signing key is not configured. An ephemeral key is generated for development.")]
    public static partial void WarnEphemeralSigningKey(this ILogger logger);

    // Request

    [LoggerMessage(Level = LogLevel.Warning, Message = "Long execution. method=[{method}], route=[{route}], elapsed=[{elapsed}]")]
    public static partial void WarnLongExecution(this ILogger logger, string method, string route, long elapsed);

    // Error

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception.")]
    public static partial void ErrorUnhandledException(this ILogger logger, Exception ex);
}
