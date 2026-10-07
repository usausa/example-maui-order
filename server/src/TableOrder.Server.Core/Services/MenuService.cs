namespace TableOrder.Server.Core.Services;

using TableOrder.Server.Core.Accessors;

public sealed class MenuService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly MenuAccessor menuAccessor;

    public MenuService(
        ServiceContextProvider contextProvider,
        MenuAccessor menuAccessor)
    {
        this.contextProvider = contextProvider;
        this.menuAccessor = menuAccessor;
    }

    // 要求した端末の店舗の今のメニュー。公開の内容 (MenuResponse の形の JSON) をそのまま返す
    public ValueTask<MenuPublicationEntity?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        return menuAccessor.QueryCurrentAsync(context.RequireTenantId(), context.RequireStoreId(), cancellationToken);
    }
}
