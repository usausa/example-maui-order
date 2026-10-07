namespace TableOrder.Server.Core.Accessors;

// 表に紐付かない処理 (スキーマとサンプルのデータの SQL ファイルの実行)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class GenericAccessor
{
    [DirectSql]
    [Execute]
    public partial ValueTask<int> ExecuteSchemaAsync(DbConnection con, string sql, CancellationToken cancellationToken);

    // サンプルのデータの SQL を実行する。@now を実行の時刻、@menuVersion と @menuContent をサンプルのメニューに束縛する
    [DirectSql]
    [Execute]
    public partial ValueTask<int> ExecuteScriptAsync(DbTransaction tx, string sql, DateTimeOffset now, string menuVersion, string menuContent, CancellationToken cancellationToken);
}
