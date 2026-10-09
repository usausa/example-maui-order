namespace TableOrder.Server.Web.Application.Context;

// Blazor の境界 (回線単位)。利用者の扱える範囲で選んだテナントと店舗の文脈にする (選んでいなければテナントも店舗もない文脈)
public sealed class BlazorServiceScope
{
    private readonly TimeProvider timeProvider;

    private readonly ApplicationServiceContextProvider provider;

    private readonly StoreSelection selection;

    // イベントごとのデリゲート生成を避けるため 1 回だけ作る
    private readonly Func<ServiceContext> factory;

    public BlazorServiceScope(
        TimeProvider timeProvider,
        ApplicationServiceContextProvider provider,
        StoreSelection selection)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.selection = selection;
        factory = CreateContext;
    }

    public IDisposable Begin() => provider.Begin(factory);

    private ServiceContext CreateContext() =>
        new(timeProvider.GetUtcNow())
        {
            TenantId = selection.TenantId,
            StoreId = selection.StoreId
        };
}
