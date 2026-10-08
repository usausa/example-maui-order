namespace TableOrder.Server.Core.Accessors;

public sealed class SqlTenantConditionTests
{
    // テナントを持たない表 (Tenants) と、テナントを決めるための引き当てと、表に紐付かない処理と、テナントをまたぐ裏の処理だけは、テナントの条件がなくてよい
    private static readonly string[] ExemptAccessors = ["GenericAccessor", "TenantAccessor", "DirectoryAccessor", "BackgroundAccessor"];

    // すべての SQL がテナントの値を引数で受けて絞る (書き忘れると、ほかのテナントの行を読み書きする)
    [Fact]
    public void EverySqlIsScopedByTenant()
    {
        // Arrange
        var files = Directory.GetFiles(SqlDirectory(), "*.sql");

        // Act
        var missing = files
            .Where(static x => !ExemptAccessors.Contains(Path.GetFileName(x).Split('.')[0]))
            .Where(static x => !File.ReadAllText(x).Contains("/*@ tenantId */", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        // Assert
        Assert.NotEmpty(files);
        Assert.Empty(missing);
    }

    // Core の SQL の置き場所 (テストの実行場所からサーバのソリューションのフォルダまでたどる)
    private static string SqlDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while ((directory is not null) && !File.Exists(Path.Combine(directory.FullName, "TableOrder.Server.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "TableOrder.Server.Core", "Accessors", "Sql");
    }
}
