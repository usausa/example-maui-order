namespace TableOrder.Server.Core.Infrastructure.Data;

public sealed class GuidTextConverterTests
{
    // GUID は小文字のハイフン付きで書く (大文字と小文字が混ざると、文字列の比較で行を引けない)
    [Fact]
    public void WritesLowercase()
    {
        // Arrange
        var id = new Guid(4002, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        // Act
        var text = GuidTextConverter.ToDb(id);

        // Assert
        Assert.Equal("00000fa2-0000-0000-0000-000000000000", text);
        Assert.Equal(id, GuidTextConverter.FromDb(text));
    }
}
