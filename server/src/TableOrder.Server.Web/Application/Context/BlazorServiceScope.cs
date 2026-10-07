namespace TableOrder.Server.Web.Application.Context;

// Blazor の境界 (回線単位)。管理画面はまだサインインがないので、テナントを持たない運営者の文脈にする
public sealed class BlazorServiceScope
{
    private readonly TimeProvider timeProvider;

    private readonly ApplicationServiceContextProvider provider;

    // イベントごとのデリゲート生成を避けるため 1 回だけ作る
    private readonly Func<ServiceContext> factory;

    public BlazorServiceScope(
        TimeProvider timeProvider,
        ApplicationServiceContextProvider provider)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        factory = CreateContext;
    }

    public IDisposable Begin() => provider.Begin(factory);

    private ServiceContext CreateContext() => new(timeProvider.GetUtcNow());
}
