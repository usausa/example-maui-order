namespace TableOrder.Terminal.Diagnostics;

using System.Text.Json;

public sealed record CrashInfo
{
    public required string Id { get; init; }

    public required DateTimeOffset Time { get; init; }

    public required string Version { get; init; }

    public required string Device { get; init; }

    public required string ExceptionType { get; init; }

    public required string Message { get; init; }

    public required string Detail { get; init; }

    public bool Shown { get; init; }
}

// 落ちたときの例外を記録し、次の起動でログに残す (お客様の画面には出さず、起動を待たせない)
public static partial class CrashReport
{
    private static Exception? lastException;

    public static void Start()
    {
        AppDomain.CurrentDomain.UnhandledException += static (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogException(ex);
            }
        };

        PlatformStart();
    }

    // 観測されなかったタスクの例外は、落ちていないのでログにだけ残す (前回の異常終了にしない)
    public static void WatchUnobserved(ILogger log) =>
        TaskScheduler.UnobservedTaskException += (_, args) => log.WarnUnobservedTaskException(args.Exception);

    // 前回の異常終了をログに残し、残したものとして記録する
    public static void LogPrevious(ILogger log)
    {
        if (Load() is not { Shown: false } info)
        {
            return;
        }

        log.WarnPreviousCrash(info.Time, info.Version, info.ExceptionType, info.Detail);
        Save(info with { Shown = true });
    }

    private static partial void PlatformStart();

    private static partial string ResolveCrashPath();

    public static void LogException(Exception e)
    {
        if (ReferenceEquals(Interlocked.Exchange(ref lastException, e), e))
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            var device = DeviceInfo.Current;
            var info = new CrashInfo
            {
                Id = Guid.NewGuid().ToString(),
                Time = DateTimeOffset.Now,
                Version = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})",
                Device = $"{device.Manufacturer} {device.Model} / {device.Platform} {device.VersionString}",
                ExceptionType = e.GetType().FullName ?? e.GetType().Name,
                Message = e.Message,
                Detail = e.ToString()
            };
            Save(info);
        }
        catch
        {
            // 記録できなくても、落ちる処理は止めない
        }
#pragma warning restore CA1031
    }

    private static CrashInfo? Load()
    {
        var path = ResolveCrashPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CrashInfo>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Save(CrashInfo info) =>
        File.WriteAllText(ResolveCrashPath(), JsonSerializer.Serialize(info));
}
