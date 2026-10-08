namespace TableOrder.Server.Core.Services;

using System.Collections.Concurrent;

using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

public sealed class MenuService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly MenuAccessor menuAccessor;

    // 店舗ごとの今のメニュー。公開の内容は変わらないので、店舗の指す公開が変わったときだけ読み直す
    private readonly ConcurrentDictionary<(Guid TenantId, Guid StoreId), MenuCatalog> catalogs = new();

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

    // 業務の確かめに使う店舗の今のメニュー (公開していなければ null)
    public async ValueTask<MenuCatalog?> GetCatalogAsync(StoreEntity store, CancellationToken cancellationToken)
    {
        if (store.MenuPublicationId is not { } publicationId)
        {
            return null;
        }

        var key = (store.TenantId, store.Id);
        if (catalogs.TryGetValue(key, out var cached) && (cached.PublicationId == publicationId))
        {
            return cached;
        }

        var publication = await menuAccessor.QueryAsync(store.TenantId, publicationId, cancellationToken);
        if (publication is null)
        {
            return null;
        }

        var catalog = new MenuCatalog(publicationId, JsonSerializer.Deserialize<MenuResponse>(publication.Content, JsonDefaults.Options)!);
        catalogs[key] = catalog;
        return catalog;
    }
}
