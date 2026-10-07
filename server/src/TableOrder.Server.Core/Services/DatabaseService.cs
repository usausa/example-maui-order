namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

public sealed class DatabaseService
{
    private readonly TimeProvider timeProvider;

    private readonly IDbProvider provider;

    private readonly GenericAccessor genericAccessor;

    private readonly TenantAccessor tenantAccessor;

    public DatabaseService(
        TimeProvider timeProvider,
        IDbProvider provider,
        GenericAccessor genericAccessor,
        TenantAccessor tenantAccessor)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.genericAccessor = genericAccessor;
        this.tenantAccessor = tenantAccessor;
    }

    // スキーマは起動のたびに実行する (CREATE TABLE IF NOT EXISTS なので何度実行してもよい)
    public async ValueTask InitializeAsync(string schemaPath, CancellationToken cancellationToken)
    {
        var schema = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        await provider.UsingAsync(con => genericAccessor.ExecuteSchemaAsync(con, schema, cancellationToken), cancellationToken);
    }

    // サンプルのデータ (開発とテストで使うテナント) は、テナントが 1 つもないときだけ入れる
    public async ValueTask<bool> LoadSampleDataAsync(string sqlPath, string menuPath, CancellationToken cancellationToken)
    {
        if (await tenantAccessor.CountAsync(cancellationToken) > 0)
        {
            return false;
        }

        var sql = await File.ReadAllTextAsync(sqlPath, cancellationToken);

        // メニューは API の応答と同じ形の JSON にそろえてから入れる (応答は公開の内容をそのまま返す)
        var menu = JsonSerializer.Deserialize<MenuResponse>(await File.ReadAllTextAsync(menuPath, cancellationToken), JsonDefaults.Options)!;
        var content = JsonSerializer.Serialize(menu, JsonDefaults.Options);

        var now = timeProvider.GetUtcNow();
        await provider.UsingTxAsync(async (_, tx) =>
        {
            await genericAccessor.ExecuteScriptAsync(tx, sql, now, menu.MenuVersion, content, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);

        return true;
    }
}
