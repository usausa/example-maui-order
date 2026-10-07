namespace TableOrder.Client;

public sealed class DependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.Maui",
        "TableOrder.Server",
        "TableOrder.Terminal"
    ];

    // TableOrder.Client は画面と端末・サーバのプロジェクトに依存しない (どの端末からも同じ窓口を使う)
    [Fact]
    public void ClientDoesNotReferenceApplications()
    {
        // Arrange
        var references = typeof(ITableApi).Assembly
            .GetReferencedAssemblies()
            .Select(static x => x.Name!)
            .ToList();

        // Act
        var forbidden = references
            .Where(static x => ForbiddenPrefixes.Any(prefix => x.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        // Assert
        Assert.Empty(forbidden);
    }
}
