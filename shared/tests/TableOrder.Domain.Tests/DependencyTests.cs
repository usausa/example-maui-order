namespace TableOrder.Domain;

public sealed class DependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.Maui",
        "TableOrder.Client",
        "TableOrder.Contract",
        "TableOrder.Server",
        "TableOrder.Terminal"
    ];

    // TableOrder.Domain は画面・通信・通信のデータに依存しない (端末とサーバで同じ計算を使う)
    [Fact]
    public void DomainDoesNotReferenceOtherLayers()
    {
        // Arrange
        var references = typeof(Pricing).Assembly
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
