namespace TableOrder.Server.Web.Infrastructure.Logging;

using Microsoft.AspNetCore.HttpLogging;

// 決めた経路 (秘密を運ぶ要求と応答) は、本文をログに出さない (本文を出す設定でも、方法・経路・状態・時間だけを出す)
public sealed class BodyExcludingHttpLoggingInterceptor : IHttpLoggingInterceptor
{
    private readonly Func<PathString, bool> excluded;

    public BodyExcludingHttpLoggingInterceptor(Func<PathString, bool> excluded)
    {
        this.excluded = excluded;
    }

    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        if (excluded(logContext.HttpContext.Request.Path))
        {
            logContext.LoggingFields &= ~(HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext) => ValueTask.CompletedTask;
}
